using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Infrastructure.Camera;

public sealed class CameraStreamService : ICameraStreamService
{
    private readonly AppSettings _appSettings;
    private readonly ICameraDecoderFactory _decoderFactory;
    private readonly LatestCameraFrameCache _frameCache;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private ICameraDecoder? _decoder;
    private CameraRuntimeSettings? _activeSettings;
    private int _operationId;
    private int _framesRendered;
    private bool _disposed;
    private Guid _activeSessionId;
    private DateTimeOffset? _lastValidFrameAt;
    private DateTimeOffset? _lastDecodedFrameAt;
    private DateTimeOffset? _lastCachedFrameAt;
    private DateTimeOffset? _lastUiRenderedFrameAt;
    private DateTimeOffset? _decoderStartedAt;
    private DateTimeOffset? _lastSuccessfulConnectAt;

    public CameraStreamService(
        AppSettings appSettings,
        ICameraDecoderFactory decoderFactory,
        LatestCameraFrameCache frameCache)
    {
        _appSettings = appSettings;
        _decoderFactory = decoderFactory;
        _frameCache = frameCache;
    }

    public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;
    public bool HasReceivedFirstFrame { get; private set; }
    public bool HasRenderedFirstFrame { get; private set; }
    public CameraDecodedFrame? LatestFrame { get; private set; }
    public int FramesDecoded => _decoder?.FramesDecoded ?? 0;
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
    public int? ActiveFfmpegPid => _decoder?.ProcessId;
    public bool IsFfmpegProcessAlive => _decoder?.IsProcessAlive == true;

    public event EventHandler<CameraDecodedFrame>? FrameDecoded;
    public event EventHandler? FirstFrameReceived;
    public event EventHandler<CameraConnectionState>? StateChanged;
    public event EventHandler<string>? DecoderUnexpectedExit;

    public async Task<bool> ConnectAsync(
        CameraRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectInternalAsync(cancellationToken).ConfigureAwait(false);
            return await ConnectInternalAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReleaseConnectionGate();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReleaseConnectionGate();
        }
    }

    public Task<bool> ApplySettingsAndReconnectAsync(
        CameraRuntimeSettings settings,
        CancellationToken cancellationToken = default) =>
        ConnectAsync(settings, cancellationToken);

    public async Task<ConnectionTestResult> TestAsync(
        CameraRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var operationId = Interlocked.Increment(ref _operationId);
        ICameraDecoder? testDecoder = null;
        var started = DateTimeOffset.UtcNow;

        try
        {
            if (!settings.IsEnabled)
                return ConnectionTestResult.Failed("Camera đang tắt trong cấu hình.");

            if (string.IsNullOrWhiteSpace(settings.RtspHost))
                return ConnectionTestResult.Failed("Chưa nhập Host/IP camera.");

            if (_appSettings.SimulateCameraFailure)
                return ConnectionTestResult.Failed("Không kết nối được RTSP (mô phỏng lỗi).");

            testDecoder = _decoderFactory.CreateDecoder();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(FfmpegRtspDecoder.GetConnectTimeoutSeconds(settings)));

            var frameTcs = new TaskCompletionSource<CameraDecodedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            testDecoder.FrameDecoded += (_, frame) => frameTcs.TrySetResult(frame);

            await testDecoder.StartAsync(settings, operationId, timeoutCts.Token).ConfigureAwait(false);

            try
            {
                var frame = await frameTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                var elapsed = DateTimeOffset.UtcNow - started;
                CameraConnectionLogger.Write(
                    operationId,
                    "TestSuccess",
                    sanitizedEndpoint: FfmpegRtspDecoder.SanitizeEndpoint(settings),
                    resolution: $"{frame.Width}×{frame.Height}",
                    firstDecodedFrameAt: frame.CapturedAt,
                    snapshot: $"latency={elapsed.TotalSeconds:F1}s size={frame.JpegBytes.Length}");
                return ConnectionTestResult.Succeeded(
                    $"Đã nhận hình ảnh {frame.Width}×{frame.Height} sau {elapsed.TotalSeconds:F1} giây.");
            }
            catch (TimeoutException)
            {
                return ConnectionTestResult.Failed("Kết nối RTSP được nhưng chưa nhận được hình ảnh.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ConnectionTestResult.Failed("Kết nối RTSP được nhưng chưa nhận được hình ảnh.");
            }
        }
        catch (Exception ex)
        {
            CameraConnectionLogger.Write(operationId, "TestError", error: ex.Message);
            return ConnectionTestResult.Failed(ex.Message);
        }
        finally
        {
            if (testDecoder is not null)
                await testDecoder.DisposeAsync().ConfigureAwait(false);
            ReleaseConnectionGate();
        }
    }

    public async Task<CameraSnapshotResult?> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var latest = _frameCache.GetLatestCopy();
        if (latest is null)
        {
            CameraConnectionLogger.Write(_operationId, "SnapshotFail", snapshot: "no cached frame");
            return null;
        }

        var age = latest.Age(DateTimeOffset.UtcNow);
        if (age > CameraSnapshotPolicy.MaxFreshFrameAge
            || !CameraSnapshotPolicy.IsValidFileSize(latest.JpegBytes.Length)
            || !CameraSnapshotPolicy.IsSnapshotDimensions(latest.Width, latest.Height)
            || !CameraSnapshotPolicy.LooksLikeJpeg(latest.JpegBytes))
        {
            CameraConnectionLogger.Write(
                _operationId,
                "SnapshotFail",
                snapshot: $"stale_or_invalid age={age.TotalSeconds:F2}s size={latest.JpegBytes.Length}");
            return null;
        }

        CameraConnectionLogger.Write(
            _operationId,
            "SnapshotSuccess",
            resolution: $"{latest.Width}×{latest.Height}",
            snapshot: $"size={latest.JpegBytes.Length} age={age.TotalSeconds:F2}s source=LatestFrameCache");

        return await Task.FromResult(new CameraSnapshotResult
        {
            ImageBytes = latest.JpegBytes.ToArray(),
            Width = latest.Width,
            Height = latest.Height,
            ContentType = "image/jpeg",
            FrameAge = age
        }).ConfigureAwait(false);
    }

    public void NotifyPreviewRendered(int width, int height)
    {
        if (!HasReceivedFirstFrame || width < CameraSnapshotPolicy.MinValidWidth || height < CameraSnapshotPolicy.MinValidHeight)
            return;

        _framesRendered++;
        _lastUiRenderedFrameAt = DateTimeOffset.UtcNow;
        if (HasRenderedFirstFrame)
            return;

        HasRenderedFirstFrame = true;
        _lastSuccessfulConnectAt = _lastCachedFrameAt ?? _lastDecodedFrameAt;
        SetState(CameraConnectionState.Connected);
        FirstFrameReceived?.Invoke(this, EventArgs.Empty);
        CameraConnectionLogger.Write(
            _operationId,
            "FirstRenderedFrame",
            resolution: $"{width}×{height}",
            firstRenderedFrameAt: DateTimeOffset.UtcNow,
            framesRendered: _framesRendered);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _lifetimeCts.CancelAsync().ConfigureAwait(false);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (_connectionGate.Wait(0))
            {
                try
                {
                    await DisconnectInternalAsync(CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    _connectionGate.Release();
                }

                break;
            }

            await Task.Delay(20, CancellationToken.None).ConfigureAwait(false);
        }

        _lifetimeCts.Dispose();
        _connectionGate.Dispose();
    }

    private async Task<bool> ConnectInternalAsync(
        CameraRuntimeSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.IsEnabled)
        {
            SetState(CameraConnectionState.Disabled);
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.RtspHost))
        {
            SetState(CameraConnectionState.Failed);
            return false;
        }

        if (_appSettings.SimulateCameraFailure)
        {
            SetState(CameraConnectionState.Failed);
            return false;
        }

        _activeSettings = settings;
        HasReceivedFirstFrame = false;
        HasRenderedFirstFrame = false;
        _framesRendered = 0;
        LatestFrame = null;
        _lastValidFrameAt = null;
        _lastDecodedFrameAt = null;
        _lastCachedFrameAt = null;
        _lastUiRenderedFrameAt = null;
        _activeSessionId = Guid.NewGuid();
        _decoderStartedAt = DateTimeOffset.UtcNow;
        var operationId = Interlocked.Increment(ref _operationId);
        SetState(CameraConnectionState.Connecting);

        _decoder = _decoderFactory.CreateDecoder();
        _decoder.StateChanged += OnDecoderStateChanged;
        _decoder.FrameDecoded += OnDecoderFrameDecoded;

        try
        {
            await _decoder.StartAsync(settings, operationId, cancellationToken).ConfigureAwait(false);

            using var firstFrameCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCts.Token);
            firstFrameCts.CancelAfter(TimeSpan.FromSeconds(FfmpegRtspDecoder.GetConnectTimeoutSeconds(settings)));

            var decodeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var renderTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnDecode(object? _, CameraDecodedFrame __) => decodeTcs.TrySetResult(true);
            void OnRendered(object? _, EventArgs __) => renderTcs.TrySetResult(true);

            _decoder.FrameDecoded += OnDecode;
            FirstFrameReceived += OnRendered;
            try
            {
                if (!HasReceivedFirstFrame)
                    await decodeTcs.Task.WaitAsync(firstFrameCts.Token).ConfigureAwait(false);

                if (!HasRenderedFirstFrame)
                    await renderTcs.Task.WaitAsync(firstFrameCts.Token).ConfigureAwait(false);

                return HasRenderedFirstFrame;
            }
            finally
            {
                _decoder.FrameDecoded -= OnDecode;
                FirstFrameReceived -= OnRendered;
            }
        }
        catch (OperationCanceledException) when (_disposed || cancellationToken.IsCancellationRequested)
        {
            await DisconnectInternalAsync(CancellationToken.None).ConfigureAwait(false);
            return false;
        }
        catch (Exception ex)
        {
            CameraConnectionLogger.Write(operationId, "ConnectError", error: ex.Message);
            SetState(CameraConnectionState.Failed);
            await DisconnectInternalAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }
    }

    private async Task DisconnectInternalAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_decoder is not null)
        {
            _decoder.StateChanged -= OnDecoderStateChanged;
            _decoder.FrameDecoded -= OnDecoderFrameDecoded;
            await _decoder.DisposeAsync().ConfigureAwait(false);
            _decoder = null;
        }

        _activeSettings = null;
        HasReceivedFirstFrame = false;
        HasRenderedFirstFrame = false;
        _framesRendered = 0;
        _decoderStartedAt = null;
        SetState(CameraConnectionState.Disconnected);
    }

    private void OnDecoderStateChanged(object? sender, CameraConnectionState state)
    {
        if (state == CameraConnectionState.Failed)
        {
            SetState(CameraConnectionState.Failed);
            DecoderUnexpectedExit?.Invoke(this, "Decoder reported failure");
            return;
        }

        if (!HasRenderedFirstFrame)
            SetState(CameraConnectionState.Connecting);
        else if (state == CameraConnectionState.Connected)
            SetState(CameraConnectionState.Connected);
    }

    private void OnDecoderFrameDecoded(object? sender, CameraDecodedFrame frame)
    {
        if (frame.SessionId != Guid.Empty && frame.SessionId != _activeSessionId)
            return;

        var sessionFrame = frame.SessionId == Guid.Empty
            ? new CameraDecodedFrame
            {
                JpegBytes = frame.JpegBytes,
                Width = frame.Width,
                Height = frame.Height,
                CapturedAt = frame.CapturedAt,
                Codec = frame.Codec,
                SessionId = _activeSessionId
            }
            : frame;

        LatestFrame = sessionFrame;
        _lastDecodedFrameAt = DateTimeOffset.UtcNow;
        if (_frameCache.TryPublish(sessionFrame, _activeSessionId))
            _lastCachedFrameAt = _lastDecodedFrameAt;

        _lastValidFrameAt = _lastCachedFrameAt ?? _lastDecodedFrameAt;

        if (!HasReceivedFirstFrame)
        {
            HasReceivedFirstFrame = true;
            CameraConnectionLogger.Write(
                _operationId,
                "PipelineFirstFrame",
                resolution: $"{sessionFrame.Width}×{sessionFrame.Height}",
                firstDecodedFrameAt: sessionFrame.CapturedAt,
                framesDecoded: _decoder?.FramesDecoded,
                snapshot: $"jpegBytes={sessionFrame.JpegBytes.Length}; session={_activeSessionId}");
        }

        FrameDecoded?.Invoke(this, sessionFrame);
    }

    private void ReleaseConnectionGate()
    {
        if (_disposed)
            return;

        _connectionGate.Release();
    }

    private void SetState(CameraConnectionState state)
    {
        if (State == state)
            return;

        State = state;
        StateChanged?.Invoke(this, state);
    }
}
