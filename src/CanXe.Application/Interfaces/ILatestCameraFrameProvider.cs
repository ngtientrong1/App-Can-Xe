using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface ILatestCameraFrameProvider
{
    LatestCameraFrame? GetLatestCopy();
    long FrameSequence { get; }
    DateTimeOffset? LatestCapturedAt { get; }
    TimeSpan? LatestFrameAge { get; }

    event EventHandler<LatestCameraFrame>? FrameUpdated;

    Task<LatestCameraFrame?> WaitForFreshFrameAsync(
        TimeSpan maxFrameAge,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken = default);
}
