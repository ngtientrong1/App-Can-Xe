using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private bool _disconnectedByUser;
    private bool _hasReceivedHardwareFrame;
    private CancellationTokenSource? _autoConnectCts;
    private int _autoConnectGeneration;
    private int _connectionOperationId;
    private readonly SemaphoreSlim _vmConnectGate = new(1, 1);

    [ObservableProperty] private bool _isScaleConnecting;
    [ObservableProperty] private string _operatorStatusMessage = string.Empty;

    public string OperatorBarText =>
        !string.IsNullOrWhiteSpace(OperatorStatusMessage)
            ? OperatorStatusMessage
            : StatusMessage ?? string.Empty;
    [ObservableProperty] private double _windowWidth = 1920;
    [ObservableProperty] private double _windowHeight = 1080;

    public double WorkspaceMaxHeight => OperatorLayoutMetrics.GetWorkspaceMaxHeight(WindowHeight);
    public double WorkspaceMinHeight => OperatorLayoutMetrics.GetWorkspaceMinHeight(WindowHeight);
    public double DataGridMinHeight => OperatorLayoutMetrics.GetDataGridMinHeight(WindowHeight);
    public double LiveWeightViewboxMaxHeight => OperatorLayoutMetrics.GetLiveWeightViewboxMaxHeight(WindowHeight);
    public double FormFieldHeight => OperatorLayoutMetrics.GetFormFieldHeight(WindowHeight);
    public double NotesFieldHeight => OperatorLayoutMetrics.GetNotesFieldHeight(WindowHeight);
    public double SummaryFooterMaxHeight => OperatorLayoutMetrics.SummaryFooterMaxHeight;
    public double TicketGridRowHeight => OperatorLayoutMetrics.DataGridRowHeight;

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

    public void UpdateWindowSize(double width, double height)
    {
        if (width > 0)
            WindowWidth = width;

        if (height > 0)
            WindowHeight = height;

        IsCompactMode = CompactLayoutPolicy.ShouldUseCompactMode(WindowWidth);
        NotifyWorkAreaLayoutChanged();
    }

    public void UpdateWindowWidth(double width) => UpdateWindowSize(width, WindowHeight);

    public async Task AutoConnectScaleIfNeededAsync()
    {
        if (!ShouldAutoConnectOnStartup())
            return;

        _autoConnectCts?.Cancel();
        _autoConnectCts = new CancellationTokenSource();
        var token = _autoConnectCts.Token;
        var generation = Interlocked.Increment(ref _autoConnectGeneration);

        ScaleConnectionLogger.Write(
            Interlocked.Increment(ref _connectionOperationId),
            "AutoConnect:scheduled",
            _hardwareScale?.IsConnected == true,
            null,
            null,
            null,
            ScaleInputMode.ToString(),
            _developerModeEnabled);

        for (var attempt = 0; attempt < ScaleAutoConnectPolicy.MaxRetryAttempts; attempt++)
        {
            if (token.IsCancellationRequested
                || generation != _autoConnectGeneration
                || _disconnectedByUser
                || ScaleInputMode != ScaleInputMode.Hardware)
                return;

            if (_hardwareScale?.IsConnected == true)
                return;

            var delay = ScaleAutoConnectPolicy.GetRetryDelay(attempt);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, token).ConfigureAwait(false);

            if (await ConnectHardwareInternalAsync(token, attempt).ConfigureAwait(false))
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

    private async Task<bool> ConnectHardwareInternalAsync(
        CancellationToken cancellationToken = default,
        int? retryNumber = null)
    {
        if (_hardwareScale is null || ScaleInputMode != ScaleInputMode.Hardware)
            return false;

        await _vmConnectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var operationId = Interlocked.Increment(ref _connectionOperationId);
        try
        {
            if (_hardwareScale.IsConnected)
                return true;

            IsScaleConnecting = true;
            _hasReceivedHardwareFrame = false;
            RefreshLiveWeightDisplay();
            OnPropertyChanged(nameof(IsRetryConnectVisible));

            IsHardwareConnectEnabled = false;
            var settings = BuildHardwareSerialSettings();
            var portOpenBefore = _hardwareScale.IsConnected;

            ScaleConnectionLogger.Write(
                operationId,
                "Connect:start",
                portOpenBefore,
                $"{settings.PortName} @ {settings.BaudRate}",
                null,
                retryNumber,
                ScaleInputMode.ToString(),
                _developerModeEnabled);

            try
            {
                await _hardwareScale.PrepareAndConnectHardwareAsync(settings, cancellationToken).ConfigureAwait(false);
                if (!_hardwareScale.IsConnected)
                {
                    OperatorStatusMessage = $"Không mở được {settings.PortName}";
                    ScaleConnectionLogger.Write(
                        operationId,
                        "Connect:failed",
                        false,
                        $"{settings.PortName} @ {settings.BaudRate}",
                        false,
                        retryNumber,
                        ScaleInputMode.ToString(),
                        _developerModeEnabled,
                        "Not connected after PrepareAndConnect");
                    return false;
                }

                OperatorStatusMessage = $"Đã kết nối {settings.PortName} @ {settings.BaudRate}";
                ScaleConnectionLogger.Write(
                    operationId,
                    "Connect:succeeded",
                    false,
                    $"{settings.PortName} @ {settings.BaudRate}",
                    true,
                    retryNumber,
                    ScaleInputMode.ToString(),
                    _developerModeEnabled);
                LogScaleModeState("ConnectHardware");
                return true;
            }
            catch (OperationCanceledException)
            {
                ScaleConnectionLogger.Write(
                    operationId,
                    "Connect:cancelled",
                    portOpenBefore,
                    $"{settings.PortName} @ {settings.BaudRate}",
                    null,
                    retryNumber,
                    ScaleInputMode.ToString(),
                    _developerModeEnabled);
                return false;
            }
            catch (Exception ex)
            {
                var port = settings.PortName;
                OperatorStatusMessage = $"Không mở được {port}: {ex.Message}";
                ScaleConnectionLogger.Write(
                    operationId,
                    "Connect:failed",
                    portOpenBefore,
                    $"{settings.PortName} @ {settings.BaudRate}",
                    false,
                    retryNumber,
                    ScaleInputMode.ToString(),
                    _developerModeEnabled,
                    ex.Message);
                return false;
            }
        }
        finally
        {
            IsScaleConnecting = false;
            UpdateHardwareDiagnostics();
            RefreshLiveWeightDisplay();
            OnPropertyChanged(nameof(IsRetryConnectVisible));
            _vmConnectGate.Release();
        }
    }

    private void RefreshLiveWeightDisplay()
    {
        OnPropertyChanged(nameof(LiveWeightDisplayText));
        OnPropertyChanged(nameof(ShowLiveWeightUnit));
    }
}
