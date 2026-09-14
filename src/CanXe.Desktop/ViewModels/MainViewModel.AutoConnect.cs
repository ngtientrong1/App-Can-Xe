using System.IO;
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
    private CancellationTokenSource? _watchdogCts;
    private int _autoConnectGeneration;
    private int _connectionOperationId;
    private int _reconnectRetryCount;
    private DateTimeOffset? _waitingForDataSince;
    private bool _loggedFirstFrame;
    private bool _loggedDataAlive;
    private readonly SemaphoreSlim _vmConnectGate = new(1, 1);
    private readonly ScaleReconnectGate _reconnectGate = new();

    [ObservableProperty] private bool _isScaleConnecting;
    [ObservableProperty] private string _operatorStatusMessage = string.Empty;
    [ObservableProperty] private bool _isScalePortOpen;
    [ObservableProperty] private bool _isScaleDataAlive;

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

    /// <summary>
    /// Manual retry stays available whenever hardware is not receiving live data.
    /// </summary>
    public bool IsRetryConnectVisible =>
        ScaleInputMode == ScaleInputMode.Hardware
        && !IsScaleConnecting
        && !_reconnectGate.IsInProgress
        && !IsScaleDataAlive;

    public bool ShowLiveWeightUnit =>
        ScaleInputMode != ScaleInputMode.Hardware
        || (_hasReceivedHardwareFrame && _hardwareScale is { IsConnected: true, IsStale: false, LatestReading: not null });

    public string LiveWeightDisplayText
    {
        get
        {
            if (ScaleInputMode == ScaleInputMode.Hardware)
            {
                if (IsScaleConnecting || _reconnectGate.IsInProgress)
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

        ScaleWatchdogLogger.Write("ScaleConnectRequested", "AutoConnect");
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
                || ScaleInputMode != ScaleInputMode.Hardware
                || _isShuttingDown)
                return;

            // Port open alone is not enough — keep going until data is alive or attempts exhausted.
            if (IsScaleDataAlive)
                return;

            if (_hardwareScale?.IsConnected == true && !IsScaleDataAlive)
            {
                StartScaleWatchdog();
                return;
            }

            var delay = ScaleAutoConnectPolicy.GetRetryDelay(attempt);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, token).ConfigureAwait(false);

            if (await ConnectHardwareInternalAsync(token, attempt).ConfigureAwait(false))
            {
                StartScaleWatchdog();
                return;
            }
        }

        StartScaleWatchdog();
    }

    private bool ShouldAutoConnectOnStartup() =>
        !_disconnectedByUser
        && ScaleAutoConnectPolicy.ShouldAutoConnectOnStartup(
            EffectiveDeviceMode,
            ScaleInputMode.ToString(),
            Settings.AutoConnectScaleOnStartup);

    [RelayCommand]
    private async Task RetryConnectHardwareAsync() =>
        await ReconnectAsync("ManualRetry").ConfigureAwait(false);

    /// <summary>
    /// Shared reconnect flow for watchdog and the manual "THỬ KẾT NỐI LẠI" button.
    /// </summary>
    public async Task ReconnectAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (_isShuttingDown || AppShutdownCoordinator.IsShuttingDown)
            return;
        if (_disconnectedByUser)
            return;
        if (_hardwareScale is null || ScaleInputMode != ScaleInputMode.Hardware)
            return;
        if (!_reconnectGate.TryEnter())
            return;

        var isManual = string.Equals(reason, "ManualRetry", StringComparison.Ordinal);
        try
        {
            if (isManual)
            {
                _disconnectedByUser = false;
                _autoConnectCts?.Cancel();
                ScaleWatchdogLogger.Write("ScaleManualReconnectRequested", reason);
            }
            else
            {
                ScaleWatchdogLogger.Write("ScaleAutoReconnectRequested", reason);
            }

            ScaleWatchdogLogger.Write("ScaleReconnectStarted", reason);
            OperatorStatusMessage = isManual
                ? "Đang kết nối lại đầu cân..."
                : "Không nhận dữ liệu, đang tự kết nối lại...";
            IsScaleConnecting = true;
            RefreshLiveWeightDisplay();
            OnPropertyChanged(nameof(IsRetryConnectVisible));

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(8));
            var token = linked.Token;

            try
            {
                await _hardwareScale.DisconnectHardwareAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is OperationCanceledException
                    or ObjectDisposedException
                    or IOException
                    or InvalidOperationException
                    or UnauthorizedAccessException)
            {
                ScaleWatchdogLogger.Write("ScaleReconnectFailed", $"{reason}:disconnect:{ex.GetType().Name}");
            }

            // A plain Close()/Open() on a fresh SerialPort is often not enough to recover a
            // wedged USB-to-serial adapter — the driver's data pipe can stay stuck across
            // process-level reconnects. Try an OS-level device reset (the software equivalent of
            // unplug/replug) before reopening; best-effort, and a no-op if not elevated.
            try
            {
                var portName = BuildHardwareSerialSettings().PortName;
                OperatorStatusMessage = "Đang thử khôi phục cổng COM...";
                var reset = await _serialPortResetter.TryResetAsync(portName, token).ConfigureAwait(false);
                ScaleWatchdogLogger.Write("ScaleUsbResetAttempted", $"{reason}:{portName}:{(reset ? "ok" : "failed")}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ScaleWatchdogLogger.Write("ScaleReconnectFailed", $"{reason}:reset:{ex.GetType().Name}");
            }

            try
            {
                await Task.Delay(ScaleWatchdogPolicy.ReconnectSettleDelay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                ScaleWatchdogLogger.Write("ScaleReconnectFailed", $"{reason}:cancelled");
                OperatorStatusMessage = "Mất kết nối đầu cân";
                return;
            }

            _hasReceivedHardwareFrame = false;
            _loggedFirstFrame = false;
            _loggedDataAlive = false;
            IsScaleDataAlive = false;

            var ok = await ConnectHardwareInternalAsync(token, _reconnectRetryCount).ConfigureAwait(false);
            if (ok)
            {
                ScaleWatchdogLogger.Write("ScaleReconnectSucceeded", reason);
                _waitingForDataSince = DateTimeOffset.Now;
                StartScaleWatchdog();
            }
            else
            {
                ScaleWatchdogLogger.Write("ScaleReconnectFailed", reason);
                _reconnectRetryCount++;
                OperatorStatusMessage = "Mất kết nối đầu cân";
                StartScaleWatchdog();
            }
        }
        finally
        {
            IsScaleConnecting = false;
            UpdateHardwareDiagnostics();
            RefreshLiveWeightDisplay();
            OnPropertyChanged(nameof(IsRetryConnectVisible));
            _reconnectGate.Exit();
        }
    }

    private void StartScaleWatchdog()
    {
        if (_isShuttingDown || AppShutdownCoordinator.IsShuttingDown)
            return;
        if (ScaleInputMode != ScaleInputMode.Hardware)
            return;
        if (_disconnectedByUser)
            return;

        // Do not restart an active watchdog (would cancel in-flight reconnect).
        if (_watchdogCts is { IsCancellationRequested: false })
            return;

        StopScaleWatchdog(logStop: false);
        _watchdogCts = new CancellationTokenSource();
        var token = _watchdogCts.Token;
        _ = Task.Run(() => RunScaleWatchdogAsync(token), token);
    }

    private void StopScaleWatchdog(bool logStop = true)
    {
        try
        {
            _watchdogCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _watchdogCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _watchdogCts = null;
        if (logStop)
            ScaleWatchdogLogger.Write("ScaleWatchdogStopped");
    }

    private async Task RunScaleWatchdogAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested
                   && !_isShuttingDown
                   && !AppShutdownCoordinator.IsShuttingDown)
            {
                try
                {
                    await Task.Delay(ScaleWatchdogPolicy.PollInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (_disconnectedByUser || ScaleInputMode != ScaleInputMode.Hardware)
                    break;

                RefreshScaleAliveState();

                if (IsScaleDataAlive)
                {
                    if (!_loggedDataAlive)
                    {
                        ScaleWatchdogLogger.Write("ScaleDataAlive");
                        _loggedDataAlive = true;
                    }

                    _reconnectRetryCount = 0;
                    _waitingForDataSince = null;
                    continue;
                }

                var portOpen = _hardwareScale?.IsConnected == true;
                IsScalePortOpen = portOpen;

                if (portOpen)
                {
                    _waitingForDataSince ??= DateTimeOffset.Now;
                    UpdateWaitingForDataStatus();

                    var shouldReconnect = ScaleWatchdogPolicy.ShouldTriggerNoDataTimeout(
                        isShuttingDown: _isShuttingDown || AppShutdownCoordinator.IsShuttingDown,
                        disconnectedByUser: _disconnectedByUser,
                        isHardwareMode: ScaleInputMode == ScaleInputMode.Hardware,
                        isReconnectInProgress: _reconnectGate.IsInProgress,
                        portOpen: portOpen,
                        isDataAlive: IsScaleDataAlive,
                        waitingForDataSince: _waitingForDataSince,
                        now: DateTimeOffset.Now);

                    if (!shouldReconnect)
                        continue;

                    ScaleWatchdogLogger.Write("ScaleNoDataTimeout", $"retry={_reconnectRetryCount}");
                    var backoff = ScaleWatchdogPolicy.GetReconnectBackoff(_reconnectRetryCount);
                    if (backoff > TimeSpan.Zero)
                    {
                        try
                        {
                            await Task.Delay(backoff, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }

                    if (token.IsCancellationRequested || _isShuttingDown || IsScaleDataAlive)
                        continue;

                    // Do not pass watchdog token — reconnect must finish even if we later refresh watchdog.
                    await ReconnectAsync("NoDataTimeout").ConfigureAwait(false);
                    _waitingForDataSince = DateTimeOffset.Now;
                }
                else if (ShouldAutoConnectOnStartup() && !_reconnectGate.IsInProgress)
                {
                    // Soft path: port closed after failure — retry with backoff, no UI freeze.
                    _waitingForDataSince ??= DateTimeOffset.Now;
                    var backoffClosed = ScaleWatchdogPolicy.GetReconnectBackoff(_reconnectRetryCount);
                    if (_waitingForDataSince is not null
                        && DateTimeOffset.Now - _waitingForDataSince.Value >= ScaleWatchdogPolicy.NoDataTimeout + backoffClosed)
                    {
                        ScaleWatchdogLogger.Write("ScaleNoDataTimeout", "port-closed");
                        await ReconnectAsync("NoDataTimeout").ConfigureAwait(false);
                        _waitingForDataSince = DateTimeOffset.Now;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            ScaleWatchdogLogger.Write("ScaleWatchdogStopped");
        }
    }

    private void RefreshScaleAliveState()
    {
        if (_hardwareScale is null || ScaleInputMode != ScaleInputMode.Hardware)
        {
            IsScalePortOpen = false;
            IsScaleDataAlive = false;
            return;
        }

        IsScalePortOpen = _hardwareScale.IsConnected;
        var alive = ScaleWatchdogPolicy.IsDataAlive(_hardwareScale.LastValidFrameAt, DateTimeOffset.Now)
                    && _hardwareScale.LatestReading is not null
                    && !_hardwareScale.IsStale;
        IsScaleDataAlive = alive;

        if (alive)
        {
            _hasReceivedHardwareFrame = true;
            if (!_loggedFirstFrame)
            {
                ScaleWatchdogLogger.Write("ScaleFirstFrameReceived");
                _loggedFirstFrame = true;
            }

            var settings = BuildHardwareSerialSettings();
            if (!OperatorStatusMessage.StartsWith("Đã kết nối", StringComparison.Ordinal)
                || OperatorStatusMessage.Contains("chờ dữ liệu", StringComparison.OrdinalIgnoreCase)
                || OperatorStatusMessage.Contains("kết nối lại", StringComparison.OrdinalIgnoreCase))
            {
                OperatorStatusMessage = ScaleWatchdogPolicy.FormatConnectedStatus(
                    settings.PortName,
                    settings.BaudRate);
            }
        }

        OnPropertyChanged(nameof(IsRetryConnectVisible));
        RefreshLiveWeightDisplay();
    }

    private void UpdateWaitingForDataStatus()
    {
        var settings = BuildHardwareSerialSettings();
        if (_reconnectGate.IsInProgress || IsScaleConnecting)
            return;

        OperatorStatusMessage = ScaleWatchdogPolicy.FormatWaitingForDataStatus(
            settings.PortName,
            settings.BaudRate);
    }

    private async Task<bool> ConnectHardwareInternalAsync(
        CancellationToken cancellationToken = default,
        int? retryNumber = null)
    {
        if (_hardwareScale is null || ScaleInputMode != ScaleInputMode.Hardware)
            return false;
        if (_isShuttingDown || AppShutdownCoordinator.IsShuttingDown)
            return false;

        await _vmConnectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var operationId = Interlocked.Increment(ref _connectionOperationId);
        try
        {
            // Only skip open when already receiving live data.
            if (_hardwareScale.IsConnected && IsScaleDataAlive)
                return true;

            IsScaleConnecting = true;
            _hasReceivedHardwareFrame = false;
            IsScaleDataAlive = false;
            RefreshLiveWeightDisplay();
            OnPropertyChanged(nameof(IsRetryConnectVisible));

            IsHardwareConnectEnabled = false;
            var settings = BuildHardwareSerialSettings();
            var portOpenBefore = _hardwareScale.IsConnected;

            ScaleWatchdogLogger.Write("ScaleConnectRequested", $"{settings.PortName}@{settings.BaudRate}");
            OperatorStatusMessage = "Đang kết nối đầu cân...";
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
                // If port is open but dead, force reopen via PrepareAndConnect (reconfigure disconnects first).
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(TimeSpan.FromSeconds(5));
                await _hardwareScale.PrepareAndConnectHardwareAsync(settings, connectCts.Token)
                    .ConfigureAwait(false);
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

                IsScalePortOpen = true;
                _waitingForDataSince = DateTimeOffset.Now;
                _loggedFirstFrame = false;
                _loggedDataAlive = false;
                OperatorStatusMessage = ScaleWatchdogPolicy.FormatWaitingForDataStatus(
                    settings.PortName,
                    settings.BaudRate);
                ScaleWatchdogLogger.Write("ScalePortOpened", $"{settings.PortName} @ {settings.BaudRate}");
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
