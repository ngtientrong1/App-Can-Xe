namespace CanXe.Domain.Entities;

public sealed class CameraDeviceSettings
{
    public int Id { get; set; } = 1;
    public string? CameraName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? RtspHost { get; set; }
    public int RtspPort { get; set; } = 554;
    public string? RtspPath { get; set; }
    public string? RtspUrl { get; set; }
    public string? Username { get; set; }
    public string? ProtectedPassword { get; set; }
    public string RtspTransport { get; set; } = "TCP";
    public bool PreviewEnabled { get; set; } = true;
    public bool AutoConnectionCheck { get; set; } = true;
    public int ConnectTimeoutSeconds { get; set; } = 5;
    public int SnapshotTimeoutSeconds { get; set; } = 5;
    public int PhotoRetentionDays { get; set; } = 3;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
