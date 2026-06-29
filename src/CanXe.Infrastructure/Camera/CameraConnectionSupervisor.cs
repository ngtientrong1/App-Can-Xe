using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Infrastructure.Camera;

public sealed class CameraConnectionSupervisor : ICameraConnectionSupervisor
{
    private readonly ICameraStreamService _stream;
    private readonly AppSettings _appSettings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CameraSupervisorOptions _options;
    private readonly Func<CancellationToken, Task<CameraRuntimeSettings>> _loadRuntimeSettings;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);

    private CancellationTokenSource? _lifetimeCts;
    private CancellationTokenSource? _watchdogCts;
    private Task? _watchdogTask;
    private CancellationTokenSource? _reconnectDelayCts;
    private bool _started;
    private bool _shuttingDown;
    private bool _userDisconnected;
    private bool _reconnectScheduled;
    private bool _pendingRestart;
    private int _operationId;

    private CameraConnectionState _state = CameraConnectionState.Disconnected;
    private int _reconnectAttempt;
    private int _reconnectSuccessCount;
    private int _stallCount;
    private int _maxSimultaneousFfmpeg;
    private string? _lastDisconnectReason;
    private DateTimeOffset? _reconnectStartedAt;

    public CameraConnectionSupervisor(
        ICameraStreamService stream,
        AppSettings appSettings,
        IServiceScopeFactory scopeFactory,
        CameraSupervisorOptions? options = null,
        Func<CancellationToken, Task<CameraRuntimeSettings>>? loadRuntimeSettings = null)
    {
        _stream = stream;
        _appSettings = appSettings;
        _scopeFactory = scopeFactory;
        _options = options ?? CameraSupervisorOptions.Default;
        _loadRuntimeSettings = loadRuntimeSettings ?? LoadRuntimeSettingsFromDbAsync;
    }

    public CameraConnectionState State => _state;
    public bool IsUserDisconnected => _userDisconnected;

    public event EventHandler<CameraConnectionState>? StateChanged;
    public event EventHandler<CameraHealthSnapshot>? HealthChanged;

    public CameraHealthSnapshot GetHealthSnapshot()
    {
        TrackFfmpegCount();
        return BuildHealthSnapshot();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        _started = true;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _stream.StateChanged += OnStreamStateChanged;
        _stream.DecoderUnexpectedExit += OnDecoderUnexpectedExit;
        StartWatchdog();

        if (await ShouldAutoConnectFromSettingsAsync(cancellationToken).ConfigureAwait(false))
            await RequestConnectAsync(CameraConnectRequestSource.Startup, _lifetimeCts.Token).ConfigureAwait(false);
    }

    public async Task RequestConnectAsync(CameraConnectRequestSource source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_shuttingDown, this);
        _userDisconnected = false;
        _reconnectDelayCts?.Cancel();

        if (!await _connectionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            if (source is CameraConnectRequestSource.Manual or CameraConnectRequestSource.DeviceTab)
                _reconnectDelayCts?.Cancel();
            await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            if (_shuttingDown)
                return;

            var operationId = Interlocked.Increment(ref _operationId);
            var isReconnect = source is CameraConnectRequestSource.Watchdog or CameraConnectRequestSource.Settings
                || _state is CameraConnectionState.Reconnecting or CameraConnectionState.Stalled or CameraConnectionState.Failed;

            if (isReconnect)
                SetSupervisorState(CameraConnectionState.Reconnecting, operationId, $"Connect requested ({source})");
            else
                SetSupervisorState(CameraConnectionState.Connecting, operationId, $"Connect requested ({source})");

            var runtime = await LoadRuntimeSettingsAsync(cancellationToken).ConfigureAwait(false);
            if (!runtime.IsEnabled)
            {
                await _stream.DisconnectAsync(cancellationToken).ConfigureAwait(false);
                SetSupervisorState(CameraConnectionState.Disabled, operationId, "Camera disabled");
                return;
            }

            var connected = await _stream.ConnectAsync(runtime, cancellationToken).ConfigureAwait(false);
            TrackFfmpegCount();

            if (connected && _stream.HasRenderedFirstFrame)
            {
                _reconnectAttempt = 0;
                _lastDisconnectReason = null;
                SetSupervisorState(CameraConnectionState.Connected, operationId, "First valid frame");
                CameraConnectionLogger.WriteStateChange(
                    operationId,
                    CameraConnectionState.Reconnecting,
                    CameraConnectionState.Connected,
                    "FirstFrameAfterReconnect",
                    _stream.ActiveSessionId,
                    _stream.ActiveFfmpegPid,
                    _stream.IsFfmpegProcessAlive,
                    _stream.FramesDecoded,
                    _stream.LastValidFrameAt,
                    _stream.LastFrameAge);
                return;
            }

            if (!connected)
            {
                ScheduleReconnect(operationId, "Connect failed", cancellationToken);
            }
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task RequestDisconnectByUserAsync(CancellationToken cancellationToken = default)
    {
        _userDisconnected = true;
        _reconnectDelayCts?.Cancel();
        _pendingRestart = false;
        _reconnectScheduled = false;

        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var operationId = Interlocked.Increment(ref _operationId);
            SetSupervisorState(CameraConnectionState.Stopping, operationId, "User disconnect");
            await _stream.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            SetSupervisorState(CameraConnectionState.Disconnected, operationId, "User disconnect completed");
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public Task RequestReconnectAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (_shuttingDown || _userDisconnected)
            return Task.CompletedTask;

        _lastDisconnectReason = reason;
        return ExecuteReconnectAsync(reason, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_shuttingDown)
            return;

        _shuttingDown = true;
        _reconnectDelayCts?.Cancel();
        _watchdogCts?.Cancel();

        if (_watchdogTask is not null)
        {
            try
            {
                await _watchdogTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stream.StateChanged -= OnStreamStateChanged;
        _stream.DecoderUnexpectedExit -= OnDecoderUnexpectedExit;

        await _connectionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var operationId = Interlocked.Increment(ref _operationId);
            SetSupervisorState(CameraConnectionState.Stopping, operationId, "App shutdown");
            await _stream.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _connectionGate.Release();
        }

        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
        _watchdogCts?.Dispose();
        _reconnectDelayCts?.Dispose();
        _connectionGate.Dispose();
    }

    private async Task<bool> ShouldAutoConnectFromSettingsAsync(CancellationToken cancellationToken)
    {
        if (_userDisconnected || _shuttingDown)
            return false;

        var runtime = await _loadRuntimeSettings(cancellationToken).ConfigureAwait(false);
        var deviceMode = _appSettings.DeviceMode;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var settings = scope.ServiceProvider.GetService<StationSettingsService>();
            if (settings is not null)
            {
                var scale = await settings.GetScaleAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(scale.DeviceMode))
                    deviceMode = scale.DeviceMode;
                else if (string.IsNullOrWhiteSpace(deviceMode))
                    deviceMode = scale.DeviceMode;
            }
        }
        catch (InvalidOperationException)
        {
            // Test hosts may use a minimal scope factory.
        }

        return CameraConnectionPolicy.ShouldAutoConnectOnStartup(
            deviceMode,
            runtime.IsEnabled,
            runtime.AutoConnectCameraOnStartup);
    }

    private Task<CameraRuntimeSettings> LoadRuntimeSettingsAsync(CancellationToken cancellationToken) =>
        _loadRuntimeSettings(cancellationToken);

    private async Task<CameraRuntimeSettings> LoadRuntimeSettingsFromDbAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<StationSettingsService>();
        return await settings.GetCameraRuntimeAsync(cancellationToken).ConfigureAwait(false);
    }

    private void StartWatchdog()
    {
        _watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts!.Token);
        var token = _watchdogCts.Token;
        _watchdogTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.WatchdogIntervalSeconds), token)
                        .ConfigureAwait(false);
                    WatchdogTick();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }, token);
    }

    private void WatchdogTick()
    {
        if (_shuttingDown || _userDisconnected)
            return;

        if (_connectionGate.CurrentCount == 0)
            return;

        TrackFfmpegCount();
        var operationId = _operationId;
        var frameAge = _stream.LastFrameAge;
        var hasValidFrame = _stream.LastValidFrameAt.HasValue;

        switch (_state)
        {
            case CameraConnectionState.Connecting or CameraConnectionState.Reconnecting:
                if (hasValidFrame && _stream.IsConnected)
                {
                    _reconnectAttempt = 0;
                    _lastDisconnectReason = null;
                    _pendingRestart = false;
                    _reconnectScheduled = false;
                    SetSupervisorState(CameraConnectionState.Connected, operationId, "Valid frame received");
                    return;
                }

                var startupAge = _stream.DecoderStartedAt.HasValue
                    ? DateTimeOffset.UtcNow - _stream.DecoderStartedAt.Value
                    : (TimeSpan?)null;

                if (!hasValidFrame
                    && startupAge > TimeSpan.FromSeconds(_options.StartupFirstFrameTimeoutSeconds)
                    && !_reconnectScheduled
                    && !_pendingRestart)
                {
                    _ = ExecuteReconnectAsync(
                        $"No first valid frame within {_options.StartupFirstFrameTimeoutSeconds}s",
                        _lifetimeCts?.Token ?? CancellationToken.None);
                }

                return;

            case CameraConnectionState.Connected:
                if (!hasValidFrame)
                    return;

                if (frameAge > TimeSpan.FromSeconds(_options.FrameStallRestartSeconds))
                {
                    if (!_reconnectScheduled && !_pendingRestart)
                    {
                        _ = ExecuteReconnectAsync(
                            $"No valid frame for {frameAge!.Value.TotalSeconds:F1}s",
                            _lifetimeCts?.Token ?? CancellationToken.None);
                    }

                    return;
                }

                if (frameAge > TimeSpan.FromSeconds(_options.FrameStallWarningSeconds))
                {
                    _stallCount++;
                    SetSupervisorState(
                        CameraConnectionState.Stalled,
                        operationId,
                        $"No valid frame for {frameAge!.Value.TotalSeconds:F1}s");
                }

                return;

            case CameraConnectionState.Stalled:
                if (!hasValidFrame)
                {
                    var stallAge = _stream.DecoderStartedAt.HasValue
                        ? DateTimeOffset.UtcNow - _stream.DecoderStartedAt.Value
                        : (TimeSpan?)null;

                    if (stallAge > TimeSpan.FromSeconds(_options.FrameStallRestartSeconds)
                        && !_reconnectScheduled
                        && !_pendingRestart)
                    {
                        _ = ExecuteReconnectAsync(
                            $"No valid frame for {stallAge!.Value.TotalSeconds:F1}s",
                            _lifetimeCts?.Token ?? CancellationToken.None);
                    }

                    return;
                }

                if (frameAge <= TimeSpan.FromSeconds(_options.FrameStallWarningSeconds))
                {
                    _reconnectScheduled = false;
                    _pendingRestart = false;
                    _reconnectDelayCts?.Cancel();
                    SetSupervisorState(CameraConnectionState.Connected, operationId, "Frame resumed before restart");
                }
                else if (frameAge > TimeSpan.FromSeconds(_options.FrameStallRestartSeconds)
                         && !_reconnectScheduled
                         && !_pendingRestart)
                {
                    _ = ExecuteReconnectAsync(
                        $"No valid frame for {frameAge!.Value.TotalSeconds:F1}s",
                        _lifetimeCts?.Token ?? CancellationToken.None);
                }

                return;

            case CameraConnectionState.Failed:
                if (!_reconnectScheduled && !_pendingRestart && !_userDisconnected)
                {
                    _ = ExecuteReconnectAsync("Retry after failed state", _lifetimeCts?.Token ?? CancellationToken.None);
                }

                return;
        }
    }

    private async Task ExecuteReconnectAsync(string reason, CancellationToken cancellationToken)
    {
        if (_shuttingDown || _userDisconnected)
            return;

        if (!await _connectionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _reconnectScheduled = true;
            return;
        }

        try
        {
            _reconnectScheduled = true;
            _pendingRestart = true;
            _reconnectAttempt++;
            _lastDisconnectReason = reason;
            var operationId = Interlocked.Increment(ref _operationId);
            var oldPid = _stream.ActiveFfmpegPid;
            var oldSession = _stream.ActiveSessionId;

            CameraConnectionLogger.WriteReconnectRequested(
                operationId,
                reason,
                _reconnectAttempt,
                oldSession,
                oldPid);

            SetSupervisorState(CameraConnectionState.Reconnecting, operationId, reason);

            _reconnectDelayCts?.Cancel();
            _reconnectDelayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = CameraConnectionPolicy.GetReconnectBackoffDelay(_reconnectAttempt - 1, _options);

            CameraConnectionLogger.Write(
                operationId,
                "ReconnectAttempt",
                snapshot: $"attempt={_reconnectAttempt}; delay={delay.TotalSeconds:F0}s");

            try
            {
                await Task.Delay(delay, _reconnectDelayCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _reconnectScheduled = false;
                _pendingRestart = false;
                return;
            }

            if (_shuttingDown || _userDisconnected)
                return;

            CameraConnectionLogger.Write(operationId, "DecoderStopStarted", snapshot: $"oldPid={oldPid}");

            await _stream.DisconnectAsync(cancellationToken).ConfigureAwait(false);

            CameraConnectionLogger.Write(
                operationId,
                "DecoderStopCompleted",
                snapshot: $"oldPid={oldPid}");

            _reconnectStartedAt = DateTimeOffset.UtcNow;
            var runtime = await LoadRuntimeSettingsAsync(cancellationToken).ConfigureAwait(false);
            if (!runtime.IsEnabled)
            {
                SetSupervisorState(CameraConnectionState.Disabled, operationId, "Camera disabled during reconnect");
                return;
            }

            CameraConnectionLogger.Write(
                operationId,
                "NewDecoderStarted",
                snapshot: $"session={_stream.ActiveSessionId}");

            var connected = await _stream.ConnectAsync(runtime, cancellationToken).ConfigureAwait(false);
            TrackFfmpegCount();

            CameraConnectionLogger.Write(
                operationId,
                "NewDecoderStarted",
                snapshot: $"pid={_stream.ActiveFfmpegPid}; session={_stream.ActiveSessionId}");

            if (connected && _stream.HasRenderedFirstFrame)
            {
                _reconnectSuccessCount++;
                _reconnectAttempt = 0;
                _lastDisconnectReason = null;
                _reconnectScheduled = false;
                _pendingRestart = false;
                var elapsed = _reconnectStartedAt.HasValue
                    ? DateTimeOffset.UtcNow - _reconnectStartedAt.Value
                    : (TimeSpan?)null;

                CameraConnectionLogger.Write(
                    operationId,
                    "FirstFrameAfterReconnect",
                    snapshot: elapsed is null ? null : $"elapsed={elapsed.Value.TotalSeconds:F1}s");

                CameraConnectionLogger.Write(
                    operationId,
                    "ReconnectSucceeded",
                    framesDecoded: _stream.FramesDecoded,
                    snapshot: $"pid={_stream.ActiveFfmpegPid}");

                SetSupervisorState(CameraConnectionState.Connected, operationId, "Reconnect succeeded");
                return;
            }

            CameraConnectionLogger.Write(operationId, "ReconnectFailed", error: reason);

            if (_reconnectAttempt >= _options.ReconnectFailureThreshold)
                SetSupervisorState(CameraConnectionState.Failed, operationId, $"Reconnect failed after {_reconnectAttempt} attempts");

            ScheduleReconnect(operationId, "Reconnect connect failed", cancellationToken);
        }
        finally
        {
            _pendingRestart = false;
            _connectionGate.Release();
        }
    }

    private void ScheduleReconnect(int operationId, string reason, CancellationToken cancellationToken)
    {
        if (_shuttingDown || _userDisconnected || _reconnectScheduled)
            return;

        _reconnectScheduled = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectInitialDelaySeconds), cancellationToken)
                    .ConfigureAwait(false);
                await ExecuteReconnectAsync(reason, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _reconnectScheduled = false;
            }
        }, cancellationToken);
    }

    private void OnStreamStateChanged(object? sender, CameraConnectionState state)
    {
        if (_shuttingDown)
            return;

        if (state == CameraConnectionState.Failed && _state is CameraConnectionState.Connected or CameraConnectionState.Stalled)
        {
            _ = RequestReconnectAsync("Decoder reported failure", _lifetimeCts?.Token ?? CancellationToken.None);
        }
    }

    private void OnDecoderUnexpectedExit(object? sender, string reason)
    {
        if (_shuttingDown || _userDisconnected)
            return;

        _ = RequestReconnectAsync(reason, _lifetimeCts?.Token ?? CancellationToken.None);
    }

    private void SetSupervisorState(CameraConnectionState newState, int operationId, string reason)
    {
        if (_state == newState)
            return;

        var previous = _state;
        _state = newState;

        CameraConnectionLogger.WriteStateChange(
            operationId,
            previous,
            newState,
            reason,
            _stream.ActiveSessionId,
            _stream.ActiveFfmpegPid,
            _stream.IsFfmpegProcessAlive,
            _stream.FramesDecoded,
            _stream.LastValidFrameAt,
            _stream.LastFrameAge);

        StateChanged?.Invoke(this, newState);
        HealthChanged?.Invoke(this, BuildHealthSnapshot());
    }

    private void TrackFfmpegCount()
    {
        if (_stream.IsFfmpegProcessAlive)
            _maxSimultaneousFfmpeg = Math.Max(_maxSimultaneousFfmpeg, 1);
    }

    private CameraHealthSnapshot BuildHealthSnapshot() =>
        new()
        {
            State = _state,
            FfmpegProcessAlive = _stream.IsFfmpegProcessAlive,
            FfmpegPid = _stream.ActiveFfmpegPid,
            ActiveSessionId = _stream.ActiveSessionId,
            FramesDecoded = _stream.FramesDecoded,
            LastValidFrameAt = _stream.LastValidFrameAt,
            LastFrameAge = _stream.LastFrameAge,
            ReconnectAttempt = _reconnectAttempt,
            ReconnectSuccessCount = _reconnectSuccessCount,
            StallCount = _stallCount,
            LastDisconnectReason = _lastDisconnectReason,
            MaximumSimultaneousFfmpegProcesses = Math.Max(_maxSimultaneousFfmpeg, _stream.IsFfmpegProcessAlive ? 1 : 0)
        };
}
