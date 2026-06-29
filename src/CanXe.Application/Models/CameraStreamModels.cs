namespace CanXe.Application.Models;

public enum CameraConnectionState
{
    Disabled,
    Disconnected,
    Connecting,
    Connected,
    Stalled,
    Reconnecting,
    Failed,
    Stopping
}

public enum CameraConnectRequestSource
{
    Startup,
    Manual,
    Watchdog,
    Settings,
    Diagnostics,
    DeviceTab
}

public sealed class CameraDecodedFrame
{
    public required byte[] JpegBytes { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public string? Codec { get; init; }
    public Guid SessionId { get; init; }
}

public sealed class CameraHealthSnapshot
{
    public required CameraConnectionState State { get; init; }
    public bool FfmpegProcessAlive { get; init; }
    public int? FfmpegPid { get; init; }
    public Guid ActiveSessionId { get; init; }
    public long FramesDecoded { get; init; }
    public DateTimeOffset? LastValidFrameAt { get; init; }
    public TimeSpan? LastFrameAge { get; init; }
    public int ReconnectAttempt { get; init; }
    public int ReconnectSuccessCount { get; init; }
    public int StallCount { get; init; }
    public string? LastDisconnectReason { get; init; }
    public int MaximumSimultaneousFfmpegProcesses { get; init; }

    public string ToDisplayText()
    {
        var lines = new List<string>
        {
            $"Trạng thái: {State}",
            $"FFmpeg PID: {FfmpegPid?.ToString() ?? "—"} ({(FfmpegProcessAlive ? "alive" : "dead")})",
            $"Session: {ActiveSessionId}",
            $"Frames decoded: {FramesDecoded}",
            $"Last valid frame: {LastValidFrameAt?.LocalDateTime:HH:mm:ss.fff} ({FormatAge(LastFrameAge)})",
            $"Reconnect attempt: {ReconnectAttempt} (successes: {ReconnectSuccessCount})",
            $"Stall count: {StallCount}",
            $"Max simultaneous FFmpeg: {MaximumSimultaneousFfmpegProcesses}"
        };

        if (!string.IsNullOrWhiteSpace(LastDisconnectReason))
            lines.Add($"Last disconnect: {LastDisconnectReason}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatAge(TimeSpan? age) =>
        age is null ? "—" : $"{age.Value.TotalSeconds:F1}s ago";
}

public sealed class CameraSnapshotResult
{
    public required byte[] ImageBytes { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string ContentType { get; init; }
    public required TimeSpan FrameAge { get; init; }
}

public sealed class CameraRuntimeSettings
{
    public bool IsEnabled { get; init; }
    public bool AutoConnectCameraOnStartup { get; init; }
    public string? RtspHost { get; init; }
    public int RtspPort { get; init; } = RtspDefaults.DefaultPort;
    public string? RtspPath { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string RtspTransport { get; init; } = RtspDefaults.DefaultTransport;
    public int ConnectTimeoutSeconds { get; init; } = 12;
    public int SnapshotTimeoutSeconds { get; init; } = 5;
    public int PhotoRetentionDays { get; init; } = 3;
    public string? CameraName { get; init; }
    public bool PreviewEnabled { get; init; } = true;

    public string BuildRtspUrl() =>
        CanXe.Domain.Services.RtspUrlBuilder.Build(
            RtspHost,
            RtspPort,
            RtspPath,
            Username,
            Password,
            RtspTransport);
}

public static class RtspDefaults
{
    public const int DefaultPort = 554;
    public const string DefaultTransport = "TCP";
}
