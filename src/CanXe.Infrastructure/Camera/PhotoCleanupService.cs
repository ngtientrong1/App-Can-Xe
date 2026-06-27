using CanXe.Application.Interfaces;

namespace CanXe.Infrastructure.Camera;

public sealed class PhotoCleanupService : IPhotoCleanupService
{
    private readonly string _photoRoot;
    private readonly TimeSpan _retention;

    public PhotoCleanupService(string photoRoot, TimeSpan? retention = null)
    {
        _photoRoot = photoRoot;
        _retention = retention ?? TimeSpan.FromDays(3);
    }

    public Task CleanupOldPhotosAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_photoRoot))
            return Task.CompletedTask;

        var cutoff = DateTime.Now - _retention;

        foreach (var directory in Directory.GetDirectories(_photoRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dirInfo = new DirectoryInfo(directory);
            if (dirInfo.LastWriteTime < cutoff)
            {
                try
                {
                    dirInfo.Delete(recursive: true);
                }
                catch
                {
                    // Best effort cleanup; do not affect tickets/events.
                }
            }
        }

        return Task.CompletedTask;
    }
}
