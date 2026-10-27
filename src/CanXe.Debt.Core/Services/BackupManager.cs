using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace CanXe.Debt.Services;

public sealed record BackupInfo(string FileName, string FullPath, DateTimeOffset CreatedAt, long SizeBytes, string Kind);

/// <param name="CopyError">Set when the backup itself worked but the extra copy to the second folder failed.</param>
public sealed record BackupResult(bool Ok, BackupInfo? Info, string? Error, string? CopyError = null);

/// <summary>
/// Consistent copies of the database (SQLite VACUUM INTO), each checked with an integrity test before it counts,
/// optionally mirrored to a second folder such as a USB drive or a synced cloud folder.
/// </summary>
public sealed partial class BackupManager(string databasePath, string backupDirectory)
{
    public const string Daily = "daily";
    public const string Manual = "manual";
    public const string BeforeSeasonClose = "truoc-chot-mua";

    public const int KeepDaily = 30;
    public const int KeepManual = 20;

    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(36);
    private readonly object _gate = new();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.Now;

    public string BackupDirectory { get; } = backupDirectory;

    /// <summary>Last failure from the automatic daily backup, shown on the backup page until the next success.</summary>
    public string? LastError { get; private set; }

    public BackupResult Create(string kind, string? copyDirectory = null, DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            var stamp = now ?? DateTimeOffset.Now;
            try
            {
                if (!File.Exists(databasePath))
                    return Failed("Chưa có cơ sở dữ liệu để sao lưu.");

                Directory.CreateDirectory(BackupDirectory);
                var finalPath = Path.Combine(BackupDirectory, $"debt-{stamp:yyyyMMdd-HHmmss}-{kind}.db");
                var tempPath = finalPath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                CopyDatabase(tempPath);
                if (!VerifyIntegrity(tempPath, out var problem))
                {
                    File.Delete(tempPath);
                    return Failed($"Bản sao lưu không đạt kiểm tra toàn vẹn: {problem}");
                }

                File.Move(tempPath, finalPath, overwrite: true);
                var info = ToInfo(finalPath)!;
                var copyError = MirrorTo(copyDirectory, finalPath);
                Prune();
                LastError = null;
                return new BackupResult(true, info, null, copyError);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
            {
                return Failed(ex.Message);
            }
        }

        BackupResult Failed(string error)
        {
            LastError = error;
            return new BackupResult(false, null, error);
        }
    }

    public IReadOnlyList<BackupInfo> List()
    {
        if (!Directory.Exists(BackupDirectory))
            return [];

        return Directory.GetFiles(BackupDirectory, "debt-*.db")
            .Select(ToInfo)
            .OfType<BackupInfo>()
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public BackupInfo? Latest => List().FirstOrDefault();

    public bool DailyExists(DateOnly day) =>
        List().Any(b => b.Kind == Daily && DateOnly.FromDateTime(b.CreatedAt.DateTime) == day);

    /// <summary>
    /// True when the newest backup is older than a day and a half. With no backup at all it only counts as stale
    /// once the service has been up for ten minutes, because the first automatic one is made shortly after start.
    /// </summary>
    public bool IsStale(DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.Now;
        var latest = Latest;
        return latest is null ? at - _startedAt > TimeSpan.FromMinutes(10) : at - latest.CreatedAt > StaleAfter;
    }

    public BackupInfo? Find(string fileName) =>
        List().FirstOrDefault(b => string.Equals(b.FileName, fileName, StringComparison.OrdinalIgnoreCase));

    public static bool VerifyIntegrity(string path, out string detail)
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";
            detail = Convert.ToString(command.ExecuteScalar()) ?? "";
            return detail == "ok";
        }
        catch (SqliteException ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    public static string? ValidateCopyDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        try
        {
            var full = Path.GetFullPath(directory.Trim());
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, $".canxe-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return $"Không ghi được vào thư mục này: {ex.Message}";
        }
    }

    private void CopyDatabase(string destination)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"VACUUM INTO '{destination.Replace("'", "''")}'";
        command.ExecuteNonQuery();
    }

    private static string? MirrorTo(string? copyDirectory, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(copyDirectory))
            return null;

        try
        {
            var directory = Path.GetFullPath(copyDirectory.Trim());
            Directory.CreateDirectory(directory);
            File.Copy(sourcePath, Path.Combine(directory, Path.GetFileName(sourcePath)), overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ex.Message;
        }
    }

    private void Prune()
    {
        var all = List();
        Delete(all.Where(b => b.Kind == Daily).Skip(KeepDaily));
        Delete(all.Where(b => b.Kind == Manual).Skip(KeepManual));
    }

    private static void Delete(IEnumerable<BackupInfo> old)
    {
        foreach (var file in old)
        {
            try
            {
                File.Delete(file.FullPath);
            }
            catch (IOException)
            {
            }
        }
    }

    private static BackupInfo? ToInfo(string path)
    {
        var match = FileNamePattern().Match(Path.GetFileName(path));
        if (!match.Success)
            return null;

        var info = new FileInfo(path);
        var createdAt = DateTime.TryParseExact(
            match.Groups["date"].Value + (match.Groups["time"].Success ? match.Groups["time"].Value : "000000"),
            "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var parsed)
            ? new DateTimeOffset(parsed, TimeZoneInfo.Local.GetUtcOffset(parsed))
            : new DateTimeOffset(info.LastWriteTime);
        var kind = match.Groups["kind"].Success ? match.Groups["kind"].Value : Daily;
        return new BackupInfo(info.Name, info.FullName, createdAt, info.Length, kind);
    }

    [GeneratedRegex(@"^debt-(?<date>\d{8})(?:-(?<time>\d{6}))?(?:-(?<kind>[a-z-]+))?\.db$")]
    private static partial Regex FileNamePattern();
}
