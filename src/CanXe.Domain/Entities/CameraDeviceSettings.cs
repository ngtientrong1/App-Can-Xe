namespace CanXe.Domain.Entities;

public sealed class CameraDeviceSettings
{
    public int Id { get; set; } = 1;
    public string? CameraName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? RtspUrl { get; set; }
    public string? Username { get; set; }
    public string? ProtectedPassword { get; set; }
    public bool PreviewEnabled { get; set; } = true;
    public bool AutoConnectionCheck { get; set; } = true;
    public int SnapshotTimeoutSeconds { get; set; } = 5;
    public int PhotoRetentionDays { get; set; } = 3;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
