using CanXe.Infrastructure.Camera;

namespace CanXe.Infrastructure.Diagnostics;

internal static class DiagnosticsCleanup
{
    public static async Task WaitForFfmpegDrainAsync(CancellationToken cancellationToken)
    {
        using var cleanupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cleanupCts.CancelAfter(DiagnosticsTimeouts.Cleanup);
        var started = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < DiagnosticsTimeouts.Cleanup)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FfmpegCapabilityProbe.CountFfmpegProcesses() == 0)
                return;

            await Task.Delay(200, cleanupCts.Token).ConfigureAwait(false);
        }
    }
}
