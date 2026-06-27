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
        if (!IsHardwareDeviceMode(deviceMode))
        {
            if (savedScaleInputMode == ScaleInputMode.SimulationManual)
                return ScaleInputMode.SimulationManual;
            return ScaleInputMode.SimulationAutomatic;
        }

        return savedScaleInputMode switch
        {
            ScaleInputMode.Hardware => ScaleInputMode.Hardware,
            ScaleInputMode.SimulationAutomatic => ScaleInputMode.SimulationAutomatic,
            ScaleInputMode.SimulationManual => ScaleInputMode.SimulationManual,
            _ => ScaleInputMode.Hardware
        };
    }

    public static bool IsValidModeForDevice(string deviceMode, ScaleInputMode mode)
    {
        if (mode == ScaleInputMode.Hardware)
            return IsHardwareDeviceMode(deviceMode);
        return mode is ScaleInputMode.SimulationAutomatic or ScaleInputMode.SimulationManual;
    }

    public static string GetWeightSourceText(ScaleInputMode mode) => mode switch
    {
        ScaleInputMode.SimulationAutomatic => "Nguồn: Tự động mô phỏng",
        ScaleInputMode.SimulationManual => "Nguồn: DEV thủ công",
        ScaleInputMode.Hardware => "Nguồn: Đầu cân COM",
        _ => "Nguồn: —"
    };

    public static string GetHeaderScaleBadge(ScaleInputMode mode, bool isDisconnected) =>
        isDisconnected
            ? "● Đầu cân: Mất kết nối"
            : mode switch
            {
                ScaleInputMode.SimulationAutomatic => "● Đầu cân: Tự động",
                ScaleInputMode.SimulationManual => "● Đầu cân: DEV thủ công",
                ScaleInputMode.Hardware => "● Đầu cân: COM",
                _ => "● Đầu cân: —"
            };

    public static bool ShouldPersistMode(ScaleInputMode mode) => true;

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
