using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private bool _disconnectedByUser;
    private bool _hasReceivedHardwareFrame;
    private CancellationTokenSource? _autoConnectCts;

    [ObservableProperty] private bool _isScaleConnecting;
    [ObservableProperty] private string _operatorStatusMessage = string.Empty;

    public string OperatorBarText =>
        !string.IsNullOrWhiteSpace(OperatorStatusMessage)
            ? OperatorStatusMessage
            : StatusMessage ?? string.Empty;
    [ObservableProperty] private double _windowWidth = 1920;

    public bool IsRetryConnectVisible =>
        ScaleInputMode == ScaleInputMode.Hardware
        && !IsScaleConnecting
        && !IsHardwareConnected;

    public bool ShowLiveWeightUnit =>
        ScaleInputMode != ScaleInputMode.Hardware
        || (_hasReceivedHardwareFrame && _hardwareScale is { IsConnected: true, IsStale: false, LatestReading: not null });

    public string LiveWeightDisplayText
    {
        get
        {
            if (ScaleInputMode == ScaleInputMode.Hardware)
            {
                if (IsScaleConnecting)
                    return "ĐANG KẾT NỐI";
                if (_hardwareScale is null || !_hardwareScale.IsConnected)
                    return "—";
                if (!_hasReceivedHardwareFrame || _hardwareScale.LatestReading is null)
                    return "—";
                return LiveWeightKg.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
            }

            return LiveWeightKg.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        }
    }

    public double LiveWeightUnitFontSize =>
        WorkAreaLayoutCalculator.GetLiveWeightUnitFontSize(WindowWidth);

    public void UpdateWindowWidth(double width)
    {
        if (width <= 0)
            return;

        WindowWidth = width;
        IsCompactMode = CompactLayoutPolicy.ShouldUseCompactMode(width);
        NotifyWorkAreaLayoutChanged();
        OnPropertyChanged(nameof(LiveWeightFontSize));
        OnPropertyChanged(nameof(LiveWeightUnitFontSize));
    }

    public async Task AutoConnectScaleIfNeededAsync()
    {
        if (!ShouldAutoConnectOnStartup())
            return;

        _autoConnectCts?.Cancel();
        _autoConnectCts = new CancellationTokenSource();
        var token = _autoConnectCts.Token;

        for (var attempt = 0; attempt < ScaleAutoConnectPolicy.MaxRetryAttempts; attempt++)
        {
            if (token.IsCancellationRequested || _disconnectedByUser)
                return;

            var delay = ScaleAutoConnectPolicy.GetRetryDelay(attempt);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, token).ConfigureAwait(false);

            if (await ConnectHardwareInternalAsync(token).ConfigureAwait(false))
                return;
        }
    }

    private bool ShouldAutoConnectOnStartup() =>
        !_disconnectedByUser
        && ScaleAutoConnectPolicy.ShouldAutoConnectOnStartup(
            EffectiveDeviceMode,
            ScaleInputMode.ToString(),
            Settings.AutoConnectScaleOnStartup);

    [RelayCommand]
    private async Task RetryConnectHardwareAsync()
    {
        _disconnectedByUser = false;
        _autoConnectCts?.Cancel();
        await ConnectHardwareInternalAsync().ConfigureAwait(false);
    }

    private async Task<bool> ConnectHardwareInternalAsync(CancellationToken cancellationToken = default)
    {
        if (_hardwareScale is null || ScaleInputMode != ScaleInputMode.Hardware)
            return false;

        IsScaleConnecting = true;
        _hasReceivedHardwareFrame = false;
        RefreshLiveWeightDisplay();
        OnPropertyChanged(nameof(IsRetryConnectVisible));

        try
        {
            IsHardwareConnectEnabled = false;
            var settings = BuildHardwareSerialSettings();
            await _hardwareScale.PrepareAndConnectHardwareAsync(settings, cancellationToken).ConfigureAwait(false);
            if (!_hardwareScale.IsConnected)
            {
                OperatorStatusMessage = $"Không mở được {settings.PortName}";
                return false;
            }

            OperatorStatusMessage = $"Đã kết nối {settings.PortName} @ {settings.BaudRate}";
            LogScaleModeState("ConnectHardware");
            return true;
        }
        catch (Exception ex)
        {
            var port = BuildHardwareSerialSettings().PortName;
            OperatorStatusMessage = $"Không mở được {port}: {ex.Message}";
            return false;
        }
        finally
        {
            IsScaleConnecting = false;
            UpdateHardwareDiagnostics();
            RefreshLiveWeightDisplay();
            OnPropertyChanged(nameof(IsRetryConnectVisible));
        }
    }

    private void RefreshLiveWeightDisplay()
    {
        OnPropertyChanged(nameof(LiveWeightDisplayText));
        OnPropertyChanged(nameof(ShowLiveWeightUnit));
    }
}
