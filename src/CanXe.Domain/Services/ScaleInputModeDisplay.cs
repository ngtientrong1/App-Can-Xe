using CanXe.Domain.Models;

namespace CanXe.Domain.Services;

public static class ScaleInputModeDisplay
{
    public static bool IsHardwareDeviceMode(string deviceMode) =>
        string.Equals(deviceMode, "Hardware", StringComparison.OrdinalIgnoreCase);

    public static ScaleInputMode GetDefaultMode(string deviceMode) =>
        IsHardwareDeviceMode(deviceMode)
            ? ScaleInputMode.Hardware
            : ScaleInputMode.SimulationAutomatic;

    public static ScaleInputMode ResolveStartupMode(string deviceMode, ScaleInputMode? savedScaleInputMode)
    {
        if (IsHardwareDeviceMode(deviceMode))
            return ScaleInputMode.Hardware;

        if (savedScaleInputMode == ScaleInputMode.SimulationManual)
            return ScaleInputMode.SimulationManual;

        return ScaleInputMode.SimulationAutomatic;
    }

    public static bool ShouldNormalizeLegacyScaleMode(string deviceMode, ScaleInputMode? savedScaleInputMode) =>
        IsHardwareDeviceMode(deviceMode)
        && savedScaleInputMode.HasValue
        && savedScaleInputMode.Value != ScaleInputMode.Hardware;

    public static bool IsValidModeForDevice(string deviceMode, ScaleInputMode mode) =>
        IsValidModeForDevice(deviceMode, mode, developerModeEnabled: false);

    public static bool IsValidModeForDevice(string deviceMode, ScaleInputMode mode, bool developerModeEnabled)
    {
        if (mode == ScaleInputMode.Hardware)
            return IsHardwareDeviceMode(deviceMode);

        if (mode is ScaleInputMode.SimulationAutomatic or ScaleInputMode.SimulationManual)
            return !IsHardwareDeviceMode(deviceMode) || developerModeEnabled;

        return false;
    }

    public static bool IsSimulationSelectionEnabled(string deviceMode, bool developerModeEnabled) =>
        !IsHardwareDeviceMode(deviceMode) || developerModeEnabled;

    public static string GetWeightSourceText(ScaleInputMode mode) => mode switch
    {
        ScaleInputMode.SimulationAutomatic => "Nguồn: Tự động mô phỏng",
        ScaleInputMode.SimulationManual => "Nguồn: Thủ công mô phỏng",
        ScaleInputMode.Hardware => "Nguồn: Đầu cân COM",
        _ => "Nguồn: —"
    };

    public static string GetHeaderScaleBadge(ScaleInputMode mode, bool isDisconnected) =>
        mode == ScaleInputMode.Hardware
            ? GetHeaderScaleBadge(
                mode,
                isDisconnected ? ScaleHeaderConnectionState.Disconnected : ScaleHeaderConnectionState.Connected)
            : isDisconnected
                ? "● Đầu cân: Mất kết nối"
                : GetHeaderScaleBadge(mode, ScaleHeaderConnectionState.Connected);

    public static string GetHeaderScaleBadge(ScaleInputMode mode, ScaleHeaderConnectionState connectionState) =>
        mode switch
        {
            ScaleInputMode.SimulationAutomatic => "● Đầu cân: Tự động",
            ScaleInputMode.SimulationManual => "● Đầu cân: Thủ công",
            ScaleInputMode.Hardware when connectionState == ScaleHeaderConnectionState.Connecting =>
                "● Đầu cân: Đang kết nối",
            ScaleInputMode.Hardware when connectionState == ScaleHeaderConnectionState.WaitingForData =>
                "● Đầu cân: Đang chờ dữ liệu",
            ScaleInputMode.Hardware when connectionState == ScaleHeaderConnectionState.Disconnected =>
                "● Đầu cân: Mất kết nối",
            ScaleInputMode.Hardware => "● Đầu cân: COM",
            _ => "● Đầu cân: —"
        };

    public static bool ShouldPersistMode(ScaleInputMode mode) =>
        ShouldPersistMode("Simulation", mode);

    public static bool ShouldPersistMode(string deviceMode, ScaleInputMode mode)
    {
        if (IsHardwareDeviceMode(deviceMode))
            return mode == ScaleInputMode.Hardware;

        return mode is ScaleInputMode.Hardware or ScaleInputMode.SimulationAutomatic or ScaleInputMode.SimulationManual;
    }

    public static ScaleInputMode GetPersistedScaleInputMode(string deviceMode, ScaleInputMode activeMode) =>
        ShouldPersistMode(deviceMode, activeMode)
            ? activeMode
            : GetDefaultMode(deviceMode);

    public static bool IsManualInputEnabled(ScaleInputMode mode) =>
        mode == ScaleInputMode.SimulationManual;

    public static bool IsReturnToAutomaticVisible(ScaleInputMode mode) =>
        mode == ScaleInputMode.SimulationManual;

    public static bool IsHardwareOptionEnabled(string deviceMode) =>
        IsHardwareDeviceMode(deviceMode);

    public static bool DevDrawerCloseChangesScaleMode() => false;

    public static bool IsEditTicketWeightUnlockIndependentOfScaleMode() => true;

    public static bool IsDeveloperWeight1OverrideIndependentOfScaleMode() => true;
}
