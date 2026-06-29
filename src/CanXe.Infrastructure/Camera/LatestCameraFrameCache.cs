using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Infrastructure.Camera;

public sealed class LatestCameraFrameCache : ILatestCameraFrameProvider
{
    private readonly object _gate = new();
    private LatestCameraFrame? _latest;
    private long _sequence;

    public long FrameSequence
    {
        get { lock (_gate) return _sequence; }
    }

    public DateTimeOffset? LatestCapturedAt
    {
        get { lock (_gate) return _latest?.CapturedAt; }
    }

    public TimeSpan? LatestFrameAge
    {
        get
        {
            lock (_gate)
                return _latest is null ? null : DateTimeOffset.UtcNow - _latest.CapturedAt;
        }
    }

    public event EventHandler<LatestCameraFrame>? FrameUpdated;

    public bool TryPublish(CameraDecodedFrame frame, Guid activeSessionId)
    {
        if (frame.SessionId != Guid.Empty && frame.SessionId != activeSessionId)
            return false;

        if (!CameraSnapshotPolicy.LooksLikeJpeg(frame.JpegBytes)
            || !CameraSnapshotPolicy.IsValidFileSize(frame.JpegBytes.Length)
            || !CameraSnapshotPolicy.IsSnapshotDimensions(frame.Width, frame.Height))
            return false;

        var copy = new LatestCameraFrame
        {
            JpegBytes = frame.JpegBytes.ToArray(),
            CapturedAt = DateTimeOffset.UtcNow,
            Width = frame.Width,
            Height = frame.Height,
            CameraSessionId = activeSessionId,
            FrameSequence = Interlocked.Increment(ref _sequence)
        };

        lock (_gate)
            _latest = copy;

        FrameUpdated?.Invoke(this, copy);
        return true;
    }

    public LatestCameraFrame? GetLatestCopy()
    {
        lock (_gate)
            return _latest?.Clone();
    }

    public async Task<LatestCameraFrame?> WaitForFreshFrameAsync(
        TimeSpan maxFrameAge,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var startSequence = FrameSequence;

        while (DateTimeOffset.UtcNow - started < waitTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var latest = GetLatestCopy();
            if (latest is not null && latest.Age(DateTimeOffset.UtcNow) <= maxFrameAge)
                return latest;

            if (latest is not null && latest.FrameSequence > startSequence && latest.Age(DateTimeOffset.UtcNow) <= maxFrameAge)
                return latest;

            var tcs = new TaskCompletionSource<LatestCameraFrame?>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFrame(object? _, LatestCameraFrame frame)
            {
                if (frame.Age(DateTimeOffset.UtcNow) <= maxFrameAge)
                    tcs.TrySetResult(frame.Clone());
            }

            FrameUpdated += OnFrame;
            try
            {
                latest = GetLatestCopy();
                if (latest is not null && latest.Age(DateTimeOffset.UtcNow) <= maxFrameAge)
                    return latest;

                var remaining = waitTimeout - (DateTimeOffset.UtcNow - started);
                if (remaining <= TimeSpan.Zero)
                    break;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(remaining);
                try
                {
                    return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            finally
            {
                FrameUpdated -= OnFrame;
            }
        }

        return null;
    }
}
