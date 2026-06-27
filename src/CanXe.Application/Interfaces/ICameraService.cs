namespace CanXe.Application.Interfaces;

public sealed class CameraCaptureResult
{
    public bool Success { get; init; }
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }

    public static CameraCaptureResult Succeeded(string filePath) =>
        new() { Success = true, FilePath = filePath };

    public static CameraCaptureResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

public interface ICameraService
{
    Task<CameraCaptureResult> CaptureAsync(
        string internalTicketCode,
        int weighSequence,
        CancellationToken cancellationToken = default);
}
