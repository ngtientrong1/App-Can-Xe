using CanXe.Domain.Models;

namespace CanXe.Domain.Services;

public static class ScaleInputModeDisplay
{
    public static ScaleInputMode GetDefaultMode(string deviceMode) =>
        string.Equals(deviceMode, "Simulation", StringComparison.OrdinalIgnoreCase)
            ? ScaleInputMode.SimulationAutomatic
            : ScaleInputMode.Hardware;

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

    public static bool ShouldPersistMode(ScaleInputMode mode) => false;

    public static bool IsManualInputEnabled(ScaleInputMode mode) =>
        mode == ScaleInputMode.SimulationManual;

    public static bool IsReturnToAutomaticVisible(ScaleInputMode mode) =>
        mode == ScaleInputMode.SimulationManual;

    public static bool IsHardwareOptionEnabled(string deviceMode) =>
        string.Equals(deviceMode, "Hardware", StringComparison.OrdinalIgnoreCase);

    public static bool DevDrawerCloseChangesScaleMode() => false;

    public static bool IsEditTicketWeightUnlockIndependentOfScaleMode() => true;

    public static bool IsDeveloperWeight1OverrideIndependentOfScaleMode() => true;
}
