namespace CanXe.Application.Interfaces;

public interface IPhotoCleanupService
{
    Task CleanupOldPhotosAsync(CancellationToken cancellationToken = default);
}
