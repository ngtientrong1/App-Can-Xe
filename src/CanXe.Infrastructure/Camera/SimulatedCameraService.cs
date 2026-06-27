using CanXe.Application.Interfaces;

namespace CanXe.Infrastructure.Camera;

public sealed class SimulatedCameraService : ICameraService
{
    private readonly bool _simulateFailure;

    public SimulatedCameraService(bool simulateFailure = false) =>
        _simulateFailure = simulateFailure;

    public async Task<CameraCaptureResult> CaptureAsync(
        PhotoCaptureRequest request,
        string targetFilePath,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        if (_simulateFailure)
            return CameraCaptureResult.Failed("Camera mô phỏng lỗi.");

        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
        await File.WriteAllBytesAsync(targetFilePath, CreatePlaceholderPng(), cancellationToken);
        return CameraCaptureResult.Succeeded(targetFilePath);
    }

    private static byte[] CreatePlaceholderPng() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
