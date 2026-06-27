using CanXe.Application.Interfaces;

namespace CanXe.Infrastructure.Camera;

public sealed class SimulatedCameraService : ICameraService
{
    private readonly string _photoRoot;
    private readonly bool _simulateFailure;

    public SimulatedCameraService(string photoRoot, bool simulateFailure = false)
    {
        _photoRoot = photoRoot;
        _simulateFailure = simulateFailure;
    }

    public async Task<CameraCaptureResult> CaptureAsync(
        string internalTicketCode,
        int weighSequence,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);

        if (_simulateFailure)
            return CameraCaptureResult.Failed("Camera mô phỏng lỗi.");

        var dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
        var folder = Path.Combine(_photoRoot, dateFolder);
        Directory.CreateDirectory(folder);

        var safeCode = internalTicketCode.Replace('/', '-').Replace('\\', '-');
        var filePath = Path.Combine(folder, $"{safeCode}_W{weighSequence}.png");

        await File.WriteAllBytesAsync(filePath, CreatePlaceholderPng(), cancellationToken);
        return CameraCaptureResult.Succeeded(filePath);
    }

    private static byte[] CreatePlaceholderPng()
    {
        // Minimal valid 1x1 PNG
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    }
}
