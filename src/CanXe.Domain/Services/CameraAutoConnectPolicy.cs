namespace CanXe.Domain.Services;

public static class CameraAutoConnectPolicy
{
    public const int MaxRetryAttempts = 3;

    public static bool ShouldAutoConnectOnStartup(bool cameraEnabled, bool autoConnectCameraOnStartup) =>
        cameraEnabled && autoConnectCameraOnStartup;

    public static bool ShouldRetry(int attemptIndex) => attemptIndex < MaxRetryAttempts;

    public static TimeSpan GetRetryDelay(int attemptIndex) => attemptIndex switch
    {
        0 => TimeSpan.Zero,
        1 => TimeSpan.FromSeconds(3),
        2 => TimeSpan.FromSeconds(10),
        _ => TimeSpan.Zero
    };
}
