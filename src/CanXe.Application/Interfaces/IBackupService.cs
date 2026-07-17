using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IBackupService
{
    BackupUserSettings LoadUserSettings();
    void SaveUserSettings(BackupUserSettings settings);

    Task<BackupOperationResult> CreateBackupAsync(
        string? stationName = null,
        string? fileNamePrefix = null,
        CancellationToken cancellationToken = default);

    Task<BackupValidationResult> ValidateBackupAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default);

    Task<BackupOperationResult> RestoreBackupAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default);

    int CleanupRetention(string? backupFolder, int retentionDays);

    DateTimeOffset? GetLatestBackupTime(string? backupFolder);
}
