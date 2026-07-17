using System.Text.Json.Serialization;

namespace CanXe.Application.Models;

public sealed class BackupManifest
{
    [JsonPropertyName("appName")]
    public string AppName { get; set; } = "CanXe";

    [JsonPropertyName("backupFormatVersion")]
    public int BackupFormatVersion { get; set; } = 1;

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "1.0.1";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("stationName")]
    public string? StationName { get; set; }

    [JsonPropertyName("databaseFileName")]
    public string DatabaseFileName { get; set; } = "canxe.db";

    [JsonPropertyName("checksumSha256")]
    public string ChecksumSha256 { get; set; } = string.Empty;

    [JsonPropertyName("includesSettings")]
    public bool IncludesSettings { get; set; }
}

public sealed class BackupUserSettings
{
    public string? BackupFolderPath { get; set; }
    public bool AutoBackupEnabled { get; set; }
    public int RetentionDays { get; set; } = 30;
    public string? LastBackupAt { get; set; }
    public string? LastAutoBackupDate { get; set; }
}

public sealed class BackupOperationResult
{
    public bool Success { get; init; }
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }
    public bool RequiresRestart { get; init; }

    public static BackupOperationResult Ok(string? filePath = null, bool requiresRestart = false) =>
        new() { Success = true, FilePath = filePath, RequiresRestart = requiresRestart };

    public static BackupOperationResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public sealed class BackupValidationResult
{
    public bool Success { get; init; }
    public BackupManifest? Manifest { get; init; }
    public string? ErrorMessage { get; init; }

    public static BackupValidationResult Ok(BackupManifest manifest) =>
        new() { Success = true, Manifest = manifest };

    public static BackupValidationResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
