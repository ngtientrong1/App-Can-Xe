namespace CanXe.Domain.Services;

public sealed class CameraSupervisorOptions
{
    public int StartupFirstFrameTimeoutSeconds { get; init; } = 12;
    public int FrameStallWarningSeconds { get; init; } = 5;
    public int FrameStallRestartSeconds { get; init; } = 12;
    public int ReconnectInitialDelaySeconds { get; init; } = 1;
    public int ReconnectMaxDelaySeconds { get; init; } = 15;
    public int ReconnectFailureThreshold { get; init; } = 5;
    public int WatchdogIntervalSeconds { get; init; } = 1;

    public static CameraSupervisorOptions Default { get; } = new();
}

public static class CameraConnectionPolicy
{
    public static TimeSpan GetReconnectBackoffDelay(int attemptIndex, CameraSupervisorOptions options)
    {
        var exponent = Math.Clamp(attemptIndex, 0, 3);
        var seconds = options.ReconnectInitialDelaySeconds * Math.Pow(2, exponent);
        var capped = Math.Min(seconds, options.ReconnectMaxDelaySeconds);
        return TimeSpan.FromSeconds(capped);
    }

    public static bool ShouldAutoConnectOnStartup(
        string deviceMode,
        bool cameraEnabled,
        bool autoConnectCameraOnStartup) =>
        ProductionConfigPolicy.IsHardwareMode(deviceMode)
        && cameraEnabled
        && autoConnectCameraOnStartup;
}
