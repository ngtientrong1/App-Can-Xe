using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Camera;

public sealed class CameraSnapshotService : ICameraSnapshotService
{
    private readonly ILatestCameraFrameProvider _frameCache;
    private readonly ICameraConnectionSupervisor? _supervisor;

    public CameraSnapshotService(
        ILatestCameraFrameProvider frameCache,
        ICameraConnectionSupervisor? supervisor = null)
    {
        _frameCache = frameCache;
        _supervisor = supervisor;
    }

    public async Task<CameraSnapshotCaptureResult> CaptureFromCacheAsync(CancellationToken cancellationToken = default)
    {
        var state = _supervisor?.State ?? CameraConnectionState.Connected;
        if (state is CameraConnectionState.Stalled or CameraConnectionState.Reconnecting or CameraConnectionState.Failed)
        {
            var waited = await _frameCache.WaitForFreshFrameAsync(
                CameraSnapshotPolicy.MaxFreshFrameAge,
                CameraSnapshotPolicy.SnapshotWaitForFreshFrameTimeout,
                cancellationToken).ConfigureAwait(false);

            if (waited is null)
            {
                OperatorActionLogger.WriteSnapshotRejected(
                    "FrameTooOldOrUnavailable",
                    _frameCache.LatestFrameAge,
                    state);
                return CameraSnapshotCaptureResult.Failed(
                    "Đang chờ hình ảnh camera...",
                    "FrameTooOld",
                    state,
                    _frameCache.LatestFrameAge);
            }

            return BuildSuccess(waited, state);
        }

        var latest = _frameCache.GetLatestCopy();
        if (latest is not null && CameraSnapshotPolicy.IsSnapshotFresh(latest.CapturedAt, DateTimeOffset.UtcNow))
            return BuildSuccess(latest, state);

        var fresh = await _frameCache.WaitForFreshFrameAsync(
            CameraSnapshotPolicy.MaxFreshFrameAge,
            CameraSnapshotPolicy.SnapshotWaitForFreshFrameTimeout,
            cancellationToken).ConfigureAwait(false);

        if (fresh is null)
        {
            var age = latest?.Age(DateTimeOffset.UtcNow);
            OperatorActionLogger.WriteSnapshotRejected(
                latest is null ? "NoCachedFrame" : "FrameTooOld",
                age,
                state);
            return CameraSnapshotCaptureResult.Failed(
                latest is null ? "Không có hình ảnh camera." : "Hình ảnh camera quá cũ.",
                latest is null ? "NoCachedFrame" : "FrameTooOld",
                state,
                age);
        }

        return BuildSuccess(fresh, state);
    }

    private static CameraSnapshotCaptureResult BuildSuccess(LatestCameraFrame frame, CameraConnectionState state)
    {
        var age = frame.Age(DateTimeOffset.UtcNow);
        var snapshot = new CameraSnapshotResult
        {
            ImageBytes = frame.JpegBytes.ToArray(),
            Width = frame.Width,
            Height = frame.Height,
            ContentType = "image/jpeg",
            FrameAge = age
        };

        return CameraSnapshotCaptureResult.Succeeded(snapshot, state);
    }
}
