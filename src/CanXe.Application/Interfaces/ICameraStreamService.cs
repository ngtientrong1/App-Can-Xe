using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface ICameraStreamService : IAsyncDisposable
{
    CameraConnectionState State { get; }
    bool HasReceivedFirstFrame { get; }
    bool HasRenderedFirstFrame { get; }
    bool IsConnected => HasRenderedFirstFrame && State == CameraConnectionState.Connected;
    CameraDecodedFrame? LatestFrame { get; }
    int FramesDecoded { get; }
    TimeSpan? LastFrameAge { get; }
    DateTimeOffset? LastValidFrameAt { get; }
    DateTimeOffset? LastDecodedFrameAt { get; }
    DateTimeOffset? LastCachedFrameAt { get; }
    DateTimeOffset? LastUiRenderedFrameAt { get; }
    DateTimeOffset? DecoderStartedAt { get; }
    DateTimeOffset? LastSuccessfulConnectAt { get; }
    Guid ActiveSessionId { get; }
    int? ActiveFfmpegPid { get; }
    bool IsFfmpegProcessAlive { get; }

    event EventHandler<CameraDecodedFrame>? FrameDecoded;
    event EventHandler? FirstFrameReceived;
    event EventHandler<CameraConnectionState>? StateChanged;
    event EventHandler<string>? DecoderUnexpectedExit;

    Task<bool> ConnectAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<bool> ApplySettingsAndReconnectAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken = default);
    Task<ConnectionTestResult> TestAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken = default);
    Task<CameraSnapshotResult?> CaptureSnapshotAsync(CancellationToken cancellationToken = default);
    void NotifyPreviewRendered(int width, int height);
}
