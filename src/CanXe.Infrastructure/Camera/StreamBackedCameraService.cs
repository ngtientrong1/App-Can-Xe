using CanXe.Application.Interfaces;
using CanXe.Domain.Services;

namespace CanXe.Infrastructure.Camera;

public sealed class StreamBackedCameraService : ICameraService
{
    private readonly ICameraSnapshotService _snapshotService;

    public StreamBackedCameraService(ICameraSnapshotService snapshotService) =>
        _snapshotService = snapshotService;

    public async Task<CameraCaptureResult> CaptureAsync(
        PhotoCaptureRequest request,
        string targetFilePath,
        CancellationToken cancellationToken = default)
    {
        var capture = await _snapshotService.CaptureFromCacheAsync(cancellationToken).ConfigureAwait(false);
        if (!capture.Success || capture.Snapshot is null)
            return CameraCaptureResult.Failed(capture.ErrorMessage ?? "Snapshot failed: no fresh camera frame");

        var snapshot = capture.Snapshot;
        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
        await File.WriteAllBytesAsync(targetFilePath, snapshot.ImageBytes, cancellationToken).ConfigureAwait(false);

        if (!CameraSnapshotPolicy.IsValidFileSize(snapshot.ImageBytes.Length)
            || !CameraSnapshotPolicy.IsSnapshotDimensions(snapshot.Width, snapshot.Height))
            return CameraCaptureResult.Failed("Snapshot failed: invalid frame dimensions");

        return CameraCaptureResult.Succeeded(targetFilePath);
    }
}
