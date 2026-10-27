using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class BackupManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "canxe-backup-test-" + Guid.NewGuid().ToString("N"));
    private readonly string _dbPath;
    private readonly BackupManager _backups;

    public BackupManagerTests()
    {
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "debt.db");
        using (var db = new DebtDbContext(new DbContextOptionsBuilder<DebtDbContext>().UseSqlite($"Data Source={_dbPath};Pooling=False").Options))
        {
            db.Database.EnsureCreated();
            db.Customers.Add(new Customer { Name = "Công Ty Đại Lợi", NormalizedName = "cong ty dai loi" });
            db.SaveChanges();
        }

        _backups = new BackupManager(_dbPath, Path.Combine(_root, "backup"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }

    private static DateTimeOffset At(int day, int hour = 3) => new(2026, 10, day, hour, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Backup_ContainsTheDataAndPassesTheIntegrityCheck()
    {
        var result = _backups.Create(BackupManager.Daily, now: At(27));

        Assert.True(result.Ok, result.Error);
        Assert.True(BackupManager.VerifyIntegrity(result.Info!.FullPath, out _));
        using var connection = new SqliteConnection($"Data Source={result.Info.FullPath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Customers";
        Assert.Equal("Công Ty Đại Lợi", command.ExecuteScalar());
    }

    [Fact]
    public void BackupName_RecordsTheTimeAndKind()
    {
        var info = _backups.Create(BackupManager.Manual, now: At(27, 14)).Info!;

        Assert.Equal("debt-20261027-140000-manual.db", info.FileName);
        Assert.Equal(BackupManager.Manual, info.Kind);
        Assert.Equal(At(27, 14), info.CreatedAt);
    }

    [Fact]
    public void DailyExists_OnlyForTheDayItWasMade()
    {
        _backups.Create(BackupManager.Daily, now: At(27));
        _backups.Create(BackupManager.Manual, now: At(28));

        Assert.True(_backups.DailyExists(new DateOnly(2026, 10, 27)));
        Assert.False(_backups.DailyExists(new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void OldDailyBackupsArePruned_ButSeasonCloseBackupsAreKept()
    {
        _backups.Create(BackupManager.BeforeSeasonClose, now: At(1));
        for (var i = 0; i < BackupManager.KeepDaily + 5; i++)
            _backups.Create(BackupManager.Daily, now: At(2).AddDays(i));

        var all = _backups.List();

        Assert.Equal(BackupManager.KeepDaily, all.Count(b => b.Kind == BackupManager.Daily));
        Assert.Single(all, b => b.Kind == BackupManager.BeforeSeasonClose);
        Assert.Equal(At(2).AddDays(BackupManager.KeepDaily + 4), all[0].CreatedAt);
    }

    [Fact]
    public void BackupIsMirroredToTheSecondFolder()
    {
        var copy = Path.Combine(_root, "usb");

        var result = _backups.Create(BackupManager.Daily, copy, At(27));

        Assert.Null(result.CopyError);
        Assert.True(File.Exists(Path.Combine(copy, result.Info!.FileName)));
    }

    [Fact]
    public void UnwritableSecondFolder_ReportsAWarningButTheBackupStillCounts()
    {
        var blocker = Path.Combine(_root, "file.txt");
        File.WriteAllText(blocker, "x");

        var result = _backups.Create(BackupManager.Daily, Path.Combine(blocker, "sub"), At(27));

        Assert.True(result.Ok);
        Assert.NotNull(result.CopyError);
    }

    [Fact]
    public void MissingDatabase_FailsWithAReasonAndRemembersIt()
    {
        var manager = new BackupManager(Path.Combine(_root, "none.db"), Path.Combine(_root, "b2"));

        var result = manager.Create(BackupManager.Daily);

        Assert.False(result.Ok);
        Assert.NotNull(manager.LastError);
    }

    [Fact]
    public void CorruptFile_FailsTheIntegrityCheck()
    {
        var bad = Path.Combine(_root, "bad.db");
        File.WriteAllText(bad, "this is not a database at all, just text padding padding padding");

        Assert.False(BackupManager.VerifyIntegrity(bad, out var detail));
        Assert.NotEmpty(detail);
    }

    [Fact]
    public void Stale_WhenNewestBackupIsOlderThanADayAndAHalf()
    {
        _backups.Create(BackupManager.Daily, now: At(27));

        Assert.False(_backups.IsStale(At(28, 12)));
        Assert.True(_backups.IsStale(At(29, 20)));
    }

    [Fact]
    public void Find_OnlyReturnsFilesInsideTheBackupFolder()
    {
        var info = _backups.Create(BackupManager.Daily, now: At(27)).Info!;

        Assert.NotNull(_backups.Find(info.FileName));
        Assert.Null(_backups.Find(@"..\debt.db"));
        Assert.Null(_backups.Find("debt.db"));
    }

    [Fact]
    public void CopyDirectoryValidation_AcceptsEmptyAndWritableFoldersOnly()
    {
        Assert.Null(BackupManager.ValidateCopyDirectory(null));
        Assert.Null(BackupManager.ValidateCopyDirectory(" "));
        Assert.Null(BackupManager.ValidateCopyDirectory(Path.Combine(_root, "ok")));

        var blocker = Path.Combine(_root, "f.txt");
        File.WriteAllText(blocker, "x");
        Assert.NotNull(BackupManager.ValidateCopyDirectory(Path.Combine(blocker, "sub")));
    }
}
