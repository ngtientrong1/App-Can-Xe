namespace CanXe.Application.Interfaces;

public enum PhotoStorageKind
{
    Draft,
    Official
}

public sealed class PhotoCaptureRequest
{
    public PhotoStorageKind StorageKind { get; init; }
    public required string ReferenceCode { get; init; }
    public int WeighSequence { get; init; }
}

public interface IPhotoStorageService
{
    string GetDraftPhotoPath(string sessionId, int sequence);
    string GetOfficialPhotoPath(string internalCode, int sequence, DateTimeOffset capturedAt);
    void DeletePhotoIfExists(string? path);
    void CleanupDraftSession(string sessionId);
}
