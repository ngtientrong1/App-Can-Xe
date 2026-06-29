using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Tests.Support;

public sealed class ControllableCameraStreamService : ICameraStreamService
{
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly object _frameGate = new();
    private int _failuresBeforeSuccess;
    private int _connectAttempts;
    private int _activeConnectOperations;
    private int _maxConcurrentConnectOperations;
    private int _connectDelayMs = 50;
    private bool _disposed;
    private bool _emitFrames = true;
    private bool _processAlive;
    private CameraDecodedFrame? _latestFrame;
    private Guid _activeSessionId;
    private DateTimeOffset? _lastValidFrameAt;
    private DateTimeOffset? _lastDecodedFrameAt;
    private DateTimeOffset? _lastCachedFrameAt;
    private DateTimeOffset? _lastUiRenderedFrameAt;
    private DateTimeOffset? _decoderStartedAt;
    private DateTimeOffset? _lastSuccessfulConnectAt;
    private CancellationTokenSource? _frameLoopCts;
    private Task? _frameLoopTask;

    public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;
    public bool HasReceivedFirstFrame { get; private set; }
    public bool HasRenderedFirstFrame { get; private set; }
    public bool IsConnected => HasRenderedFirstFrame && State == CameraConnectionState.Connected;
    public CameraDecodedFrame? LatestFrame => _latestFrame;
    public int FramesDecoded { get; private set; }
    public TimeSpan? LastFrameAge =>
        (_lastCachedFrameAt ?? _lastDecodedFrameAt) is null
            ? null
            : DateTimeOffset.UtcNow - (_lastCachedFrameAt ?? _lastDecodedFrameAt)!.Value;
    public DateTimeOffset? LastValidFrameAt => _lastCachedFrameAt ?? _lastDecodedFrameAt;
    public DateTimeOffset? LastDecodedFrameAt => _lastDecodedFrameAt;
    public DateTimeOffset? LastCachedFrameAt => _lastCachedFrameAt;
    public DateTimeOffset? LastUiRenderedFrameAt => _lastUiRenderedFrameAt;
    public DateTimeOffset? DecoderStartedAt => _decoderStartedAt;
    public DateTimeOffset? LastSuccessfulConnectAt => _lastSuccessfulConnectAt;
    public Guid ActiveSessionId => _activeSessionId;
    public int? ActiveFfmpegPid => _processAlive ? 1000 + _connectAttempts : null;
    public bool IsFfmpegProcessAlive => _processAlive;
    public int ConnectAttempts => _connectAttempts;
    public int MaxConcurrentConnectOperations => _maxConcurrentConnectOperations;

    public event EventHandler<CameraDecodedFrame>? FrameDecoded;
    public event EventHandler? FirstFrameReceived;
    public event EventHandler<CameraConnectionState>? StateChanged;
    public event EventHandler<string>? DecoderUnexpectedExit;

    public void Configure(int failuresBeforeSuccess, int connectDelayMs = 50)
    {
        _failuresBeforeSuccess = failuresBeforeSuccess;
        _connectDelayMs = connectDelayMs;
    }

    public void SetEmitFrames(bool emit) => _emitFrames = emit;

    public void SimulateProcessExit()
    {
        _processAlive = false;
        SetState(CameraConnectionState.Failed);
        DecoderUnexpectedExit?.Invoke(this, "Simulated process exit");
    }

    public async Task<bool> ConnectAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var active = Interlocked.Increment(ref _activeConnectOperations);
        _maxConcurrentConnectOperations = Math.Max(_maxConcurrentConnectOperations, active);
        try
        {
            await StopFrameLoopAsync().ConfigureAwait(false);

            _connectAttempts++;
            _activeSessionId = Guid.NewGuid();
            _decoderStartedAt = DateTimeOffset.UtcNow;
            SetState(CameraConnectionState.Connecting);
            HasReceivedFirstFrame = false;
            HasRenderedFirstFrame = false;
            _lastValidFrameAt = null;
            _lastDecodedFrameAt = null;
            _lastCachedFrameAt = null;
            _lastUiRenderedFrameAt = null;

            if (_connectDelayMs > 0)
                await Task.Delay(_connectDelayMs, cancellationToken).ConfigureAwait(false);

            if (_connectAttempts <= _failuresBeforeSuccess)
            {
                SetState(CameraConnectionState.Failed);
                return false;
            }

            _processAlive = true;
            StartFrameLoop();
            await WaitForRenderedFrameAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            return HasRenderedFirstFrame;
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnectOperations);
            _connectionGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopFrameLoopAsync().ConfigureAwait(false);
            _processAlive = false;
            HasReceivedFirstFrame = false;
            HasRenderedFirstFrame = false;
            _decoderStartedAt = null;
            SetState(CameraConnectionState.Disconnected);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public Task<bool> ApplySettingsAndReconnectAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken = default) =>
        ConnectAsync(settings, cancellationToken);

    public Task<ConnectionTestResult> TestAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.RtspHost))
            return Task.FromResult(ConnectionTestResult.Failed("Chưa nhập Host/IP camera."));

        return Task.FromResult(ConnectionTestResult.Succeeded("Test OK."));
    }

    public Task<CameraSnapshotResult?> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (_latestFrame is null)
            return Task.FromResult<CameraSnapshotResult?>(null);

        return Task.FromResult<CameraSnapshotResult?>(new CameraSnapshotResult
        {
            ImageBytes = _latestFrame.JpegBytes,
            Width = _latestFrame.Width,
            Height = _latestFrame.Height,
            ContentType = "image/jpeg",
            FrameAge = DateTimeOffset.UtcNow - _latestFrame.CapturedAt
        });
    }

    public void NotifyPreviewRendered(int width, int height)
    {
        if (!HasReceivedFirstFrame)
            return;

        _lastUiRenderedFrameAt = DateTimeOffset.UtcNow;
        if (HasRenderedFirstFrame)
            return;

        HasRenderedFirstFrame = true;
        _lastSuccessfulConnectAt = _lastCachedFrameAt ?? _lastDecodedFrameAt;
        SetState(CameraConnectionState.Connected);
        FirstFrameReceived?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        _connectionGate.Dispose();
    }

    private void StartFrameLoop()
    {
        _frameLoopCts = new CancellationTokenSource();
        var token = _frameLoopCts.Token;
        _frameLoopTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && _processAlive)
            {
                if (_emitFrames)
                    EmitFrame();

                await Task.Delay(200, token).ConfigureAwait(false);
            }
        }, token);
    }

    private async Task StopFrameLoopAsync()
    {
        _frameLoopCts?.Cancel();
        if (_frameLoopTask is not null)
            await Task.WhenAny(_frameLoopTask, Task.Delay(500)).ConfigureAwait(false);
        _frameLoopCts?.Dispose();
        _frameLoopCts = null;
        _frameLoopTask = null;
    }

    private void EmitFrame()
    {
        var jpeg = SyntheticTestJpeg.Create(640, 360, FramesDecoded + 1);
        var frame = new CameraDecodedFrame
        {
            JpegBytes = jpeg,
            Width = 640,
            Height = 360,
            CapturedAt = DateTimeOffset.UtcNow,
            Codec = "mjpeg-test",
            SessionId = _activeSessionId
        };

        lock (_frameGate)
        {
            FramesDecoded++;
            _latestFrame = frame;
            HasReceivedFirstFrame = true;
            _lastDecodedFrameAt = DateTimeOffset.UtcNow;
            _lastCachedFrameAt = _lastDecodedFrameAt;
            _lastValidFrameAt = _lastCachedFrameAt;
        }

        FrameDecoded?.Invoke(this, frame);
        NotifyPreviewRendered(frame.Width, frame.Height);
    }

    private async Task WaitForRenderedFrameAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        while (!HasRenderedFirstFrame && DateTimeOffset.UtcNow - started < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    private void SetState(CameraConnectionState state)
    {
        if (State == state)
            return;

        State = state;
        StateChanged?.Invoke(this, state);
    }
}

public static class SyntheticTestJpeg
{
    public static byte[] Create(int width, int height, int seed)
    {
        if (OperatingSystem.IsWindows())
        {
            using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.Clear(System.Drawing.Color.FromArgb((seed * 37) % 255, (seed * 53) % 255, (seed * 71) % 255));
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            return ms.ToArray();
        }

        var payload = new byte[2048 + seed * 32];
        Array.Fill(payload, (byte)(seed % 255));
        return payload;
    }
}
