using System.IO;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Desktop.Tests.Support;

public sealed class ControllableCameraStreamService : ICameraStreamService
{
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private int _connectAttempts;
    private bool _disposed;
    private CameraDecodedFrame? _latestFrame;
    private Guid _activeSessionId;
    private DateTimeOffset? _lastValidFrameAt;
    private DateTimeOffset? _lastDecodedFrameAt;
    private DateTimeOffset? _lastCachedFrameAt;
    private DateTimeOffset? _lastUiRenderedFrameAt;
    private DateTimeOffset? _decoderStartedAt;
    private DateTimeOffset? _lastSuccessfulConnectAt;

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
    public int? ActiveFfmpegPid => null;
    public bool IsFfmpegProcessAlive => State == CameraConnectionState.Connected;
    public int ConnectAttempts => _connectAttempts;

    public event EventHandler<CameraDecodedFrame>? FrameDecoded;
    public event EventHandler? FirstFrameReceived;
    public event EventHandler<CameraConnectionState>? StateChanged;
    public event EventHandler<string>? DecoderUnexpectedExit;

    public async Task<bool> ConnectAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _connectAttempts++;
            _activeSessionId = Guid.NewGuid();
            _decoderStartedAt = DateTimeOffset.UtcNow;
            SetState(CameraConnectionState.Connecting);
            HasReceivedFirstFrame = false;
            HasRenderedFirstFrame = false;
            _lastValidFrameAt = null;

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            var jpeg = CreateTestJpeg(640, 360, _connectAttempts);
                _latestFrame = new CameraDecodedFrame
                {
                    JpegBytes = jpeg,
                    Width = 640,
                    Height = 360,
                    CapturedAt = DateTimeOffset.UtcNow,
                    Codec = "mjpeg-test",
                    SessionId = _activeSessionId
                };
                FramesDecoded++;
                HasReceivedFirstFrame = true;
                _lastDecodedFrameAt = DateTimeOffset.UtcNow;
                _lastCachedFrameAt = _lastDecodedFrameAt;
                _lastValidFrameAt = _lastCachedFrameAt;
                FrameDecoded?.Invoke(this, _latestFrame);
                NotifyPreviewRendered(_latestFrame.Width, _latestFrame.Height);

            return HasRenderedFirstFrame;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
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

    public Task<ConnectionTestResult> TestAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConnectionTestResult.Succeeded("Test OK."));

    public Task<CameraSnapshotResult?> CaptureSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<CameraSnapshotResult?>(_latestFrame is null ? null : new CameraSnapshotResult
        {
            ImageBytes = _latestFrame.JpegBytes,
            Width = _latestFrame.Width,
            Height = _latestFrame.Height,
            ContentType = "image/jpeg",
            FrameAge = TimeSpan.Zero
        });

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

    private void SetState(CameraConnectionState state)
    {
        if (State == state)
            return;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static byte[] CreateTestJpeg(int width, int height, int seed)
    {
        using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.FromArgb((seed * 37) % 255, (seed * 53) % 255, (seed * 71) % 255));
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        return ms.ToArray();
    }
}
