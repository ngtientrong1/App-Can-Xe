using CanXe.Domain.Models;
using CanXe.Domain.Services;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    public bool IsHardwareScaleMode
    {
        get => ScaleInputMode == ScaleInputMode.Hardware;
        set
        {
            if (!value || ScaleInputMode == ScaleInputMode.Hardware)
                return;

            _ = ApplyScaleInputModeAsync(ScaleInputMode.Hardware, userInitiated: true, persistSettings: false);
        }
    }

    public bool IsAutomaticSimulationMode
    {
        get => ScaleInputMode == ScaleInputMode.SimulationAutomatic;
        set
        {
            if (!value || ScaleInputMode == ScaleInputMode.SimulationAutomatic)
                return;

            _ = ApplyScaleInputModeAsync(ScaleInputMode.SimulationAutomatic, userInitiated: true, persistSettings: false);
        }
    }

    public bool IsManualSimulationMode
    {
        get => ScaleInputMode == ScaleInputMode.SimulationManual;
        set
        {
            if (!value || ScaleInputMode == ScaleInputMode.SimulationManual)
                return;

            _ = ApplyScaleInputModeAsync(ScaleInputMode.SimulationManual, userInitiated: true, persistSettings: false);
        }
    }

    public bool IsHardwareMode => IsHardwareScaleMode;

    public bool IsSimulationAutomaticMode => IsAutomaticSimulationMode;

    public bool IsSimulationManualMode => IsManualSimulationMode;

    public bool IsSimulationScaleSourceSelectionEnabled =>
        ScaleInputModeDisplay.IsSimulationSelectionEnabled(EffectiveDeviceMode, _developerModeEnabled);

    public bool IsManualWeightInputVisible =>
        _developerModeEnabled && ScaleInputMode == ScaleInputMode.SimulationManual;
}
