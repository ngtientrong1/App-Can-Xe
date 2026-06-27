namespace CanXe.Application.Interfaces;

public interface ICameraService
{
    Task<CameraCaptureResult> CaptureAsync(
        PhotoCaptureRequest request,
        string targetFilePath,
        CancellationToken cancellationToken = default);
}
