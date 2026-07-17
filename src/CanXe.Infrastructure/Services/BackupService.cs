using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Infrastructure.Logging;
using Microsoft.Data.Sqlite;

namespace CanXe.Infrastructure.Services;

public sealed class BackupService : IBackupService
{
    public const int SupportedFormatVersion = 1;
    public const string BackupExtension = ".canxebackup";
    public const string FilePattern = "CanXeBackup-*.canxebackup";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppPaths _paths;
    private readonly string _userSettingsPath;
    private readonly string _themeSettingsPath;
    private readonly string? _appSettingsPath;

    public BackupService(AppPaths paths)
    {
        _paths = paths;
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe");
        Directory.CreateDirectory(root);
        _userSettingsPath = Path.Combine(root, "backup-settings.json");
        _themeSettingsPath = Path.Combine(root, "theme-settings.json");
        var baseSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _appSettingsPath = File.Exists(baseSettings) ? baseSettings : null;
    }

    public BackupUserSettings LoadUserSettings()
    {
        try
        {
            if (!File.Exists(_userSettingsPath))
                return new BackupUserSettings();

            var json = File.ReadAllText(_userSettingsPath);
            return JsonSerializer.Deserialize<BackupUserSettings>(json, JsonOptions)
                   ?? new BackupUserSettings();
        }
        catch (Exception ex)
        {
            BackupLogger.Write("BACKUP_SETTINGS_LOAD_FAILED", ex.GetType().Name);
            return new BackupUserSettings();
        }
    }

    public void SaveUserSettings(BackupUserSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tmp = _userSettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _userSettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            BackupLogger.Write("BACKUP_SETTINGS_SAVE_FAILED", ex.GetType().Name);
        }
    }

    public async Task<BackupOperationResult> CreateBackupAsync(
        string? stationName = null,
        string? fileNamePrefix = null,
        CancellationToken cancellationToken = default)
    {
        BackupLogger.Write("BACKUP_STARTED", fileNamePrefix ?? "manual");
        try
        {
            var settings = LoadUserSettings();
            var folder = settings.BackupFolderPath;
            if (string.IsNullOrWhiteSpace(folder))
                return BackupOperationResult.Fail("Chưa chọn thư mục backup.");

            Directory.CreateDirectory(folder);
            if (!File.Exists(_paths.DatabasePath))
                return BackupOperationResult.Fail("Không tìm thấy database để sao lưu.");

            var stamp = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
            var prefix = string.IsNullOrWhiteSpace(fileNamePrefix) ? "CanXeBackup" : fileNamePrefix.Trim();
            var finalName = $"{prefix}-{stamp}{BackupExtension}";
            var finalPath = Path.Combine(folder, finalName);
            var tmpPath = finalPath + ".tmp";

            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            var workDir = Path.Combine(Path.GetTempPath(), "CanXeBackup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            try
            {
                var dataDir = Path.Combine(workDir, "data");
                var configDir = Path.Combine(workDir, "config");
                Directory.CreateDirectory(dataDir);
                Directory.CreateDirectory(configDir);

                var dbCopy = Path.Combine(dataDir, "canxe.db");
                await SnapshotDatabaseAsync(_paths.DatabasePath, dbCopy, cancellationToken).ConfigureAwait(false);

                var includesSettings = false;
                if (!string.IsNullOrWhiteSpace(_appSettingsPath) && File.Exists(_appSettingsPath))
                {
                    File.Copy(_appSettingsPath, Path.Combine(configDir, "settings.json"), overwrite: true);
                    includesSettings = true;
                }

                if (File.Exists(_themeSettingsPath))
                    File.Copy(_themeSettingsPath, Path.Combine(configDir, "theme-settings.json"), overwrite: true);

                var dbBytes = await File.ReadAllBytesAsync(dbCopy, cancellationToken).ConfigureAwait(false);
                var checksum = Convert.ToHexString(SHA256.HashData(dbBytes)).ToLowerInvariant();

                var version = typeof(BackupService).Assembly.GetName().Version?.ToString() ?? "0.7.0-rc1";
                var informational = typeof(BackupService).Assembly
                    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                    .FirstOrDefault()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(informational))
                    version = informational.Split('+')[0];

                var manifest = new BackupManifest
                {
                    AppName = "CanXe",
                    BackupFormatVersion = SupportedFormatVersion,
                    AppVersion = version,
                    CreatedAt = DateTimeOffset.Now.ToString("O"),
                    StationName = stationName,
                    DatabaseFileName = "canxe.db",
                    ChecksumSha256 = checksum,
                    IncludesSettings = includesSettings
                };

                await File.WriteAllTextAsync(
                    Path.Combine(workDir, "manifest.json"),
                    JsonSerializer.Serialize(manifest, JsonOptions),
                    cancellationToken).ConfigureAwait(false);

                ZipFile.CreateFromDirectory(workDir, tmpPath, CompressionLevel.Optimal, includeBaseDirectory: false);

                var validation = await ValidateBackupAsync(tmpPath, cancellationToken).ConfigureAwait(false);
                if (!validation.Success)
                {
                    TryDelete(tmpPath);
                    BackupLogger.Write("BACKUP_FAILED", validation.ErrorMessage ?? "validate");
                    return BackupOperationResult.Fail(
                        validation.ErrorMessage ?? "Không thể tạo backup. Vui lòng kiểm tra thư mục backup.");
                }

                if (File.Exists(finalPath))
                    File.Delete(finalPath);
                File.Move(tmpPath, finalPath);

                settings.LastBackupAt = DateTimeOffset.Now.ToString("O");
                SaveUserSettings(settings);

                BackupLogger.Write("BACKUP_COMPLETED", Path.GetFileName(finalPath));
                return BackupOperationResult.Ok(finalPath);
            }
            finally
            {
                TryDeleteDirectory(workDir);
                TryDelete(tmpPath);
            }
        }
        catch (Exception ex)
        {
            BackupLogger.Write("BACKUP_FAILED", $"{ex.GetType().Name}: {ex.Message}");
            return BackupOperationResult.Fail("Không thể tạo backup. Vui lòng kiểm tra thư mục backup.");
        }
    }

    public async Task<BackupValidationResult> ValidateBackupAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
                return BackupValidationResult.Fail("Không tìm thấy file backup.");

            await using var stream = File.OpenRead(backupFilePath);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var manifestEntry = zip.GetEntry("manifest.json");
            if (manifestEntry is null)
                return BackupValidationResult.Fail("Backup thiếu manifest.json.");

            await using var manifestStream = manifestEntry.Open();
            var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, JsonOptions, cancellationToken)
                           .ConfigureAwait(false);
            if (manifest is null)
                return BackupValidationResult.Fail("manifest.json không hợp lệ.");

            if (manifest.BackupFormatVersion <= 0 || manifest.BackupFormatVersion > SupportedFormatVersion)
                return BackupValidationResult.Fail("Phiên bản backup không được hỗ trợ.");

            var dbEntry = zip.GetEntry("data/canxe.db") ?? zip.GetEntry("data\\canxe.db");
            if (dbEntry is null)
                return BackupValidationResult.Fail("Backup thiếu database.");

            await using var dbStream = dbEntry.Open();
            using var ms = new MemoryStream();
            await dbStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var actual = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
            if (!string.Equals(actual, manifest.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                return BackupValidationResult.Fail("Checksum backup không khớp.");

            return BackupValidationResult.Ok(manifest);
        }
        catch (InvalidDataException)
        {
            return BackupValidationResult.Fail("File ZIP backup không hợp lệ.");
        }
        catch (Exception ex)
        {
            BackupLogger.Write("BACKUP_VALIDATE_FAILED", ex.GetType().Name);
            return BackupValidationResult.Fail("Không thể đọc file backup.");
        }
    }

    public async Task<BackupOperationResult> RestoreBackupAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        BackupLogger.Write("RESTORE_STARTED", Path.GetFileName(backupFilePath));
        var validation = await ValidateBackupAsync(backupFilePath, cancellationToken).ConfigureAwait(false);
        if (!validation.Success)
        {
            BackupLogger.Write("RESTORE_FAILED", validation.ErrorMessage ?? "validate");
            return BackupOperationResult.Fail(validation.ErrorMessage ?? "Backup không hợp lệ.");
        }

        BackupLogger.Write("RESTORE_VALIDATED", Path.GetFileName(backupFilePath));

        var safety = await CreateBackupAsync(
            validation.Manifest?.StationName,
            "BeforeRestore-CanXeBackup",
            cancellationToken).ConfigureAwait(false);
        if (safety.Success)
            BackupLogger.Write("RESTORE_SAFETY_BACKUP_CREATED", Path.GetFileName(safety.FilePath ?? string.Empty));
        else
            BackupLogger.Write("RESTORE_SAFETY_BACKUP_FAILED", safety.ErrorMessage ?? "unknown");

        var extractDir = Path.Combine(Path.GetTempPath(), "CanXeRestore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractDir);
        var currentDbBackup = _paths.DatabasePath + ".pre-restore";
        try
        {
            ZipFile.ExtractToDirectory(backupFilePath, extractDir, overwriteFiles: true);
            var restoredDb = Path.Combine(extractDir, "data", "canxe.db");
            if (!File.Exists(restoredDb))
                return BackupOperationResult.Fail("Backup thiếu database.");

            if (File.Exists(_paths.DatabasePath))
                File.Copy(_paths.DatabasePath, currentDbBackup, overwrite: true);

            SqliteConnection.ClearAllPools();
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            File.Copy(restoredDb, _paths.DatabasePath, overwrite: true);

            var restoredSettings = Path.Combine(extractDir, "config", "settings.json");
            if (File.Exists(restoredSettings) && !string.IsNullOrWhiteSpace(_appSettingsPath))
            {
                try
                {
                    File.Copy(restoredSettings, _appSettingsPath, overwrite: true);
                }
                catch
                {
                    var fallback = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CanXe",
                        "restored-appsettings.json");
                    File.Copy(restoredSettings, fallback, overwrite: true);
                }
            }

            var restoredTheme = Path.Combine(extractDir, "config", "theme-settings.json");
            if (File.Exists(restoredTheme))
            {
                try
                {
                    File.Copy(restoredTheme, _themeSettingsPath, overwrite: true);
                }
                catch
                {
                    BackupLogger.Write("RESTORE_THEME_SETTINGS_SKIPPED", Path.GetFileName(backupFilePath));
                }
            }

            TryDelete(currentDbBackup);
            BackupLogger.Write("RESTORE_COMPLETED", Path.GetFileName(backupFilePath));
            return BackupOperationResult.Ok(backupFilePath, requiresRestart: true);
        }
        catch (Exception ex)
        {
            BackupLogger.Write("RESTORE_FAILED", ex.GetType().Name);
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(currentDbBackup))
                    File.Copy(currentDbBackup, _paths.DatabasePath, overwrite: true);
            }
            catch (Exception rollbackEx)
            {
                BackupLogger.Write("RESTORE_ROLLBACK_FAILED", rollbackEx.GetType().Name);
            }

            return BackupOperationResult.Fail("Không thể khôi phục backup. Dữ liệu hiện tại đã được giữ lại.");
        }
        finally
        {
            TryDeleteDirectory(extractDir);
            TryDelete(currentDbBackup);
        }
    }

    public int CleanupRetention(string? backupFolder, int retentionDays)
    {
        if (string.IsNullOrWhiteSpace(backupFolder) || !Directory.Exists(backupFolder))
            return 0;

        var keepDays = retentionDays is 15 or 30 ? retentionDays : 30;
        var cutoff = DateTime.Now.Date.AddDays(-keepDays);
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(backupFolder, FilePattern))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime.Date < cutoff)
                {
                    info.Delete();
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                BackupLogger.Write("RETENTION_CLEANUP_FAILED", ex.GetType().Name);
            }
        }

        if (deleted > 0)
            BackupLogger.Write("RETENTION_CLEANUP", $"deleted={deleted};days={keepDays}");

        return deleted;
    }

    public DateTimeOffset? GetLatestBackupTime(string? backupFolder)
    {
        if (string.IsNullOrWhiteSpace(backupFolder) || !Directory.Exists(backupFolder))
            return null;

        DateTime? latest = null;
        foreach (var file in Directory.EnumerateFiles(backupFolder, FilePattern))
        {
            var write = File.GetLastWriteTime(file);
            if (latest is null || write > latest)
                latest = write;
        }

        return latest is null ? null : new DateTimeOffset(latest.Value);
    }

    private static async Task SnapshotDatabaseAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SqliteConnection.ClearAllPools();
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            // VACUUM INTO creates a consistent offline copy without leaving
            // a second connection locking the destination file.
            var escaped = destinationPath.Replace("'", "''", StringComparison.Ordinal);
            using (var source = new SqliteConnection($"Data Source={sourcePath}"))
            {
                source.Open();
                using var cmd = source.CreateCommand();
                cmd.CommandText = $"VACUUM INTO '{escaped}'";
                cmd.ExecuteNonQuery();
            }

            SqliteConnection.ClearAllPools();
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}

public static class BackupLogger
{
    public static string LogFilePath => CanXeLogPaths.GetLogFile("backup.log");

    public static void Write(string eventName, string? detail = null)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" | ")
                .Append(eventName);
            if (!string.IsNullOrWhiteSpace(detail))
                line.Append(" | ").Append(detail.Replace('\r', ' ').Replace('\n', ' '));
            line.AppendLine();
            SafeLogFileAppend.Append(LogFilePath, line.ToString());
        }
        catch
        {
        }
    }
}
