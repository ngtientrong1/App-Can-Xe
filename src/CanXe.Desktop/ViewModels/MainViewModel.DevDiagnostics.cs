using System.Globalization;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.ScaleProtocol.Core;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    public bool IsDeveloperModeEnabled => _developerModeEnabled;

    public string DevActiveScaleInputModeDisplay => ScaleInputMode.ToString();

    public string DevDeviceModeDisplay => EffectiveDeviceMode;

    public string DevComConnectedDisplay =>
        ScaleInputMode == ScaleInputMode.Hardware && _hardwareScale?.IsConnected == true
            ? "yes"
            : "no";

    public string DevComPortBaudDisplay =>
        $"{Settings.PortName} @ {Settings.BaudRate}";

    public string DevFrameStatusDisplay
    {
        get
        {
            if (ScaleInputMode != ScaleInputMode.Hardware || _hardwareScale is null)
                return "—";

            if (!_hardwareScale.IsConnected)
                return "not connected";

            return _hardwareScale.IsStale ? "stale" : "valid";
        }
    }

    public string DevStableDisplay
    {
        get
        {
            if (ScaleInputMode == ScaleInputMode.Hardware)
            {
                if (_hardwareScale is null || !_hardwareScale.IsConnected)
                    return "—";
                return _hardwareScale.IsStable ? "stable" : "unstable";
            }

            return SimulateScaleStable ? "stable" : "unstable";
        }
    }

    public string DevLastFrameDisplay =>
        ScaleInputMode == ScaleInputMode.Hardware
            ? _hardwareScale?.GetLatestFrameDisplay() ?? "—"
            : "—";

    public string DevLastValidReadingTimeDisplay
    {
        get
        {
            if (ScaleInputMode != ScaleInputMode.Hardware || _hardwareScale?.LastValidFrameAt is not { } at)
                return "—";

            return at.LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
        }
    }

    public string DevSimulationRunningDisplay =>
        GetActiveServiceMode() != ScaleInputMode.Hardware ? "yes" : "no";

    public string DevWeightSourceDisplay => ScaleInputModeDisplay.GetWeightSourceText(ScaleInputMode);

    public string? ManualSimulationInputError { get; private set; }

    private void NotifyDevDiagnosticsBindings()
    {
        OnPropertyChanged(nameof(DevActiveScaleInputModeDisplay));
        OnPropertyChanged(nameof(DevDeviceModeDisplay));
        OnPropertyChanged(nameof(DevComConnectedDisplay));
        OnPropertyChanged(nameof(DevComPortBaudDisplay));
        OnPropertyChanged(nameof(DevFrameStatusDisplay));
        OnPropertyChanged(nameof(DevStableDisplay));
        OnPropertyChanged(nameof(DevLastFrameDisplay));
        OnPropertyChanged(nameof(DevLastValidReadingTimeDisplay));
        OnPropertyChanged(nameof(DevSimulationRunningDisplay));
        OnPropertyChanged(nameof(DevWeightSourceDisplay));
        OnPropertyChanged(nameof(ManualSimulationInputError));
    }
}
