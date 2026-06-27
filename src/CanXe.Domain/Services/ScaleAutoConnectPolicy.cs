namespace CanXe.Domain.Services;

public static class ScaleAutoConnectPolicy
{
    public const int MaxRetryAttempts = 3;

    public static bool GetDefaultAutoConnect(string deviceMode) =>
        ScaleInputModeDisplay.IsHardwareDeviceMode(deviceMode);

    public static bool ShouldAutoConnectOnStartup(
        string deviceMode,
        string scaleInputModeName,
        bool autoConnectSetting) =>
        ScaleInputModeDisplay.IsHardwareDeviceMode(deviceMode)
        && string.Equals(scaleInputModeName, "Hardware", StringComparison.OrdinalIgnoreCase)
        && autoConnectSetting;

    public static bool ShouldRetry(int completedAttempts) =>
        completedAttempts < MaxRetryAttempts;

    public static TimeSpan GetRetryDelay(int attemptIndex) =>
        attemptIndex switch
        {
            0 => TimeSpan.Zero,
            1 => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(5)
        };
}
