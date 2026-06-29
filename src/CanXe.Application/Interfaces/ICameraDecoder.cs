using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface ICameraDecoder : IAsyncDisposable
{
    CameraConnectionState State { get; }
    bool HasDecodedFrame { get; }
    CameraDecodedFrame? LatestFrame { get; }
    string? DetectedCodec { get; }
    int? DetectedWidth { get; }
    int? DetectedHeight { get; }
    int FramesDecoded { get; }
    DateTimeOffset? FirstPacketAt { get; }
    DateTimeOffset? FirstDecodedFrameAt { get; }
    int? ProcessId { get; }
    bool IsProcessAlive { get; }

    event EventHandler<CameraDecodedFrame>? FrameDecoded;
    event EventHandler<CameraConnectionState>? StateChanged;

    Task StartAsync(CameraRuntimeSettings settings, int operationId, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface ICameraDecoderFactory
{
    ICameraDecoder CreateDecoder();
}
