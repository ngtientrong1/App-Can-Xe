using CanXe.Application.Interfaces;

namespace CanXe.Infrastructure.Camera;

public sealed class PhotoStorageService : IPhotoStorageService
{
    private readonly string _photoRoot;

    public PhotoStorageService(string photoRoot) => _photoRoot = photoRoot;

    public string GetDraftPhotoPath(string sessionId, int sequence)
    {
        var folder = Path.Combine(_photoRoot, "Draft");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{sessionId}_W{sequence}.png");
    }

    public string GetOfficialPhotoPath(string internalCode, int sequence, DateTimeOffset capturedAt)
    {
        var safeCode = internalCode.Replace('/', '-').Replace('\\', '-');
        var folder = Path.Combine(_photoRoot, capturedAt.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{safeCode}_W{sequence}.png");
    }

    public void DeletePhotoIfExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try { File.Delete(path); }
        catch { /* best effort */ }
    }

    public void CleanupDraftSession(string sessionId)
    {
        var folder = Path.Combine(_photoRoot, "Draft");
        if (!Directory.Exists(folder))
            return;

        foreach (var file in Directory.GetFiles(folder, $"{sessionId}_W*.png"))
            DeletePhotoIfExists(file);
    }
}
