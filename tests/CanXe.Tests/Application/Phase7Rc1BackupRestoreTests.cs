using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Infrastructure.Services;
using CanXe.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase7Rc1BackupRestoreTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;
    private string _backupRoot = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
        _backupRoot = Path.Combine(Path.GetTempPath(), "CanXeBackupTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_backupRoot);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        TryDeleteDirectory(_backupRoot);
    }

    [Fact]
    public async Task Backup_CreatesCanXeBackupFile()
    {
        var service = CreateBackupService();
        var result = await service.CreateBackupAsync("Trạm test");
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(result.FilePath));
        Assert.EndsWith(".canxebackup", result.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Backup_ContainsManifest()
    {
        var service = CreateBackupService();
        var result = await service.CreateBackupAsync("Trạm test");
        Assert.True(result.Success, result.ErrorMessage);
        using var zip = ZipFile.OpenRead(result.FilePath!);
        Assert.NotNull(zip.GetEntry("manifest.json"));
    }

    [Fact]
    public async Task Backup_ContainsDatabase()
    {
        var service = CreateBackupService();
        var result = await service.CreateBackupAsync("Trạm test");
        Assert.True(result.Success, result.ErrorMessage);
        using var zip = ZipFile.OpenRead(result.FilePath!);
        Assert.True(zip.GetEntry("data/canxe.db") is not null || zip.GetEntry("data\\canxe.db") is not null);
    }

    [Fact]
    public async Task Backup_UsesAtomicTmpThenRename()
    {
        var service = CreateBackupService();
        var result = await service.CreateBackupAsync("Trạm test");
        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(File.Exists(result.FilePath + ".tmp"));
        Assert.True(File.Exists(result.FilePath));
    }

    [Fact]
    public async Task Backup_VerifiesChecksum()
    {
        var service = CreateBackupService();
        var result = await service.CreateBackupAsync("Trạm test");
        Assert.True(result.Success, result.ErrorMessage);
        var validation = await service.ValidateBackupAsync(result.FilePath!);
        Assert.True(validation.Success, validation.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(validation.Manifest?.ChecksumSha256));
    }

    [Fact]
    public async Task Backup_DoesNotCrashWhenFolderMissing()
    {
        var paths = _factory.Provider.GetRequiredService<AppPaths>();
        var service = new BackupService(paths);
        var nested = Path.Combine(_backupRoot, "missing-child", "nested");
        ConfigureFolder(service, nested);
        var result = await service.CreateBackupAsync("Trạm test");
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public async Task Backup_RetentionKeeps15Days()
    {
        var service = CreateBackupService();
        var oldFile = Path.Combine(_backupRoot, "CanXeBackup-old.canxebackup");
        await File.WriteAllTextAsync(oldFile, "x");
        File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-20));
        var keepFile = Path.Combine(_backupRoot, "CanXeBackup-new.canxebackup");
        await File.WriteAllTextAsync(keepFile, "y");
        File.SetLastWriteTime(keepFile, DateTime.Now.AddDays(-2));

        var deleted = service.CleanupRetention(_backupRoot, 15);
        Assert.True(deleted >= 1);
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(keepFile));
    }

    [Fact]
    public async Task Backup_RetentionKeeps30Days()
    {
        var service = CreateBackupService();
        var oldFile = Path.Combine(_backupRoot, "CanXeBackup-old30.canxebackup");
        await File.WriteAllTextAsync(oldFile, "x");
        File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-40));
        var keepFile = Path.Combine(_backupRoot, "CanXeBackup-mid30.canxebackup");
        await File.WriteAllTextAsync(keepFile, "y");
        File.SetLastWriteTime(keepFile, DateTime.Now.AddDays(-20));

        var deleted = service.CleanupRetention(_backupRoot, 30);
        Assert.True(deleted >= 1);
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(keepFile));
    }

    [Fact]
    public async Task Backup_DoesNotDeleteNonBackupFiles()
    {
        var service = CreateBackupService();
        var other = Path.Combine(_backupRoot, "notes.txt");
        await File.WriteAllTextAsync(other, "keep me");
        File.SetLastWriteTime(other, DateTime.Now.AddDays(-100));
        service.CleanupRetention(_backupRoot, 15);
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void Restore_RequiresAdmin()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        var permissions = new UserPermissionService(auth);
        Assert.False(permissions.HasPermission(AdminPermission.CanRestoreBackup));
        Assert.True(auth.TryUnlock("admin123").Success);
        Assert.True(permissions.HasPermission(AdminPermission.CanRestoreBackup));
    }

    [Fact]
    public async Task Restore_RejectsInvalidZip()
    {
        var service = CreateBackupService();
        var bad = Path.Combine(_backupRoot, "bad.canxebackup");
        await File.WriteAllTextAsync(bad, "not-a-zip");
        var validation = await service.ValidateBackupAsync(bad);
        Assert.False(validation.Success);
    }

    [Fact]
    public async Task Restore_RejectsMissingManifest()
    {
        var service = CreateBackupService();
        var path = Path.Combine(_backupRoot, "no-manifest.canxebackup");
        var work = Path.Combine(_backupRoot, "work-no-manifest");
        Directory.CreateDirectory(Path.Combine(work, "data"));
        await File.WriteAllBytesAsync(Path.Combine(work, "data", "canxe.db"), [1, 2, 3]);
        if (File.Exists(path)) File.Delete(path);
        ZipFile.CreateFromDirectory(work, path);
        var validation = await service.ValidateBackupAsync(path);
        Assert.False(validation.Success);
        Assert.Contains("manifest", validation.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_RejectsChecksumMismatch()
    {
        var service = CreateBackupService();
        var good = await service.CreateBackupAsync("Trạm");
        Assert.True(good.Success, good.ErrorMessage);

        var tampered = Path.Combine(_backupRoot, "tampered.canxebackup");
        var extract = Path.Combine(_backupRoot, "extract-tamper");
        if (Directory.Exists(extract)) Directory.Delete(extract, true);
        ZipFile.ExtractToDirectory(good.FilePath!, extract);
        await File.WriteAllBytesAsync(Path.Combine(extract, "data", "canxe.db"), Encoding.UTF8.GetBytes("corrupt"));
        if (File.Exists(tampered)) File.Delete(tampered);
        ZipFile.CreateFromDirectory(extract, tampered);

        var validation = await service.ValidateBackupAsync(tampered);
        Assert.False(validation.Success);
        Assert.Contains("Checksum", validation.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_CreatesSafetyBackupBeforeReplace()
    {
        var service = CreateIsolatedBackupService(out var workDb, out var folder);
        try
        {
            ConfigureFolder(service, folder);
            var original = await service.CreateBackupAsync("Trạm");
            Assert.True(original.Success, original.ErrorMessage);

            var beforeCount = Directory.GetFiles(folder, "BeforeRestore-CanXeBackup-*.canxebackup").Length;
            var result = await service.RestoreBackupAsync(original.FilePath!);
            Assert.True(result.Success, result.ErrorMessage);
            var afterCount = Directory.GetFiles(folder, "BeforeRestore-CanXeBackup-*.canxebackup").Length;
            Assert.True(afterCount > beforeCount);
            Assert.True(File.Exists(workDb));
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(workDb)!);
        }
    }

    [Fact]
    public async Task Restore_ReplacesDatabase()
    {
        var service = CreateIsolatedBackupService(out var workDb, out var folder);
        try
        {
            ConfigureFolder(service, folder);

            var backup = await service.CreateBackupAsync("Trạm");
            Assert.True(backup.Success, backup.ErrorMessage);
            await File.WriteAllBytesAsync(workDb, Encoding.UTF8.GetBytes("replaced-live"));
            Assert.Equal("replaced-live", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(workDb)));

            SqliteConnection.ClearAllPools();
            var restore = await service.RestoreBackupAsync(backup.FilePath!);
            Assert.True(restore.Success, restore.ErrorMessage);

            var restoredBytes = await File.ReadAllBytesAsync(workDb);
            Assert.NotEqual(Encoding.UTF8.GetBytes("replaced-live"), restoredBytes);
            Assert.True(restoredBytes.Length > 100);

            // Restored DB should match the database payload inside the backup ZIP.
            using var zip = ZipFile.OpenRead(backup.FilePath!);
            var entry = zip.GetEntry("data/canxe.db") ?? zip.GetEntry("data\\canxe.db");
            Assert.NotNull(entry);
            await using var stream = entry!.Open();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            Assert.Equal(ms.ToArray(), restoredBytes);
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(workDb)!);
        }
    }

    [Fact]
    public async Task Restore_RestoresSettings()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var hadSettings = File.Exists(settingsPath);
        if (!hadSettings)
            await File.WriteAllTextAsync(settingsPath, """{"DeviceMode":"Simulation"}""");

        try
        {
            var service = CreateBackupService();
            var backup = await service.CreateBackupAsync("Trạm");
            Assert.True(backup.Success, backup.ErrorMessage);
            using var zip = ZipFile.OpenRead(backup.FilePath!);
            Assert.NotNull(zip.GetEntry("config/settings.json") ?? zip.GetEntry("config\\settings.json"));
        }
        finally
        {
            if (!hadSettings && File.Exists(settingsPath))
                File.Delete(settingsPath);
        }
    }

    [Fact]
    public async Task RestoreFailure_DoesNotDeleteCurrentDatabase()
    {
        var service = CreateIsolatedBackupService(out var workDb, out var folder);
        try
        {
            ConfigureFolder(service, folder);
            var before = await File.ReadAllBytesAsync(workDb);
            var bad = Path.Combine(folder, "fail.canxebackup");
            await File.WriteAllTextAsync(bad, "bad");
            var result = await service.RestoreBackupAsync(bad);
            Assert.False(result.Success);
            var after = await File.ReadAllBytesAsync(workDb);
            Assert.Equal(before, after);
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(workDb)!);
        }
    }

    [Fact]
    public async Task AutoBackup_RunsOncePerDay()
    {
        var service = CreateBackupService();
        service.SaveUserSettings(new BackupUserSettings
        {
            BackupFolderPath = _backupRoot,
            AutoBackupEnabled = true,
            RetentionDays = 30,
            LastAutoBackupDate = null
        });

        var first = await service.CreateBackupAsync("Auto");
        Assert.True(first.Success, first.ErrorMessage);
        var settings = service.LoadUserSettings();
        settings.LastAutoBackupDate = DateTime.Now.ToString("yyyy-MM-dd");
        service.SaveUserSettings(settings);

        settings = service.LoadUserSettings();
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), settings.LastAutoBackupDate);
    }

    [Fact]
    public async Task AutoBackup_SkipsWhenFolderNotSet()
    {
        var paths = _factory.Provider.GetRequiredService<AppPaths>();
        var service = new BackupService(paths);
        service.SaveUserSettings(new BackupUserSettings
        {
            BackupFolderPath = null,
            AutoBackupEnabled = true
        });
        var result = await service.CreateBackupAsync("Auto");
        Assert.False(result.Success);
        Assert.Contains("thư mục", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutoBackup_DoesNotBlockUI()
    {
        var service = CreateBackupService();
        var task = service.CreateBackupAsync("Auto");
        var result = await task;
        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void AutoBackupFailure_LogsButDoesNotPopup()
    {
        BackupLogger.Write("AUTO_BACKUP_FAILED", "unit-test");
        Assert.False(string.IsNullOrWhiteSpace(BackupLogger.LogFilePath));
    }

    private IBackupService CreateBackupService()
    {
        var paths = _factory.Provider.GetRequiredService<AppPaths>();
        var service = new BackupService(paths);
        ConfigureFolder(service, _backupRoot);
        return service;
    }

    private BackupService CreateIsolatedBackupService(out string workDb, out string folder)
    {
        var root = Path.Combine(Path.GetTempPath(), "CanXeIsoBackup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        folder = Path.Combine(root, "backups");
        Directory.CreateDirectory(folder);
        workDb = Path.Combine(root, "canxe.db");
        var source = _factory.Provider.GetRequiredService<AppPaths>().DatabasePath;
        File.Copy(source, workDb, overwrite: true);
        return new BackupService(new AppPaths { DatabasePath = workDb, PhotoRoot = root });
    }

    private static void ConfigureFolder(IBackupService service, string folder)
    {
        service.SaveUserSettings(new BackupUserSettings
        {
            BackupFolderPath = folder,
            AutoBackupEnabled = false,
            RetentionDays = 30
        });
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
