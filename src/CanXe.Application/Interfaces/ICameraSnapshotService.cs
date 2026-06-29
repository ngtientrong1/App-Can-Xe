using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public sealed class CameraSnapshotCaptureResult
{
    public bool Success { get; init; }
    public CameraSnapshotResult? Snapshot { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RejectReason { get; init; }
    public TimeSpan? FrameAge { get; init; }
    public CameraConnectionState CameraState { get; init; }

    public static CameraSnapshotCaptureResult Succeeded(CameraSnapshotResult snapshot, CameraConnectionState state) =>
        new() { Success = true, Snapshot = snapshot, CameraState = state };

    public static CameraSnapshotCaptureResult Failed(
        string message,
        string? reason = null,
        CameraConnectionState state = CameraConnectionState.Disconnected,
        TimeSpan? frameAge = null) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            RejectReason = reason,
            CameraState = state,
            FrameAge = frameAge
        };
}

public interface ICameraSnapshotService
{
    Task<CameraSnapshotCaptureResult> CaptureFromCacheAsync(CancellationToken cancellationToken = default);
}
