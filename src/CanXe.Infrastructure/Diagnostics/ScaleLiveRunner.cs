using CanXe.Application.Interfaces;
using CanXe.Domain.Models;
using CanXe.ScaleProtocol.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Infrastructure.Diagnostics;

public static class ScaleLiveRunner
{
    public static async Task<int> RunAsync(IServiceProvider services, int seconds, bool verboseFrames)
    {
        var scale = services.GetRequiredService<IHardwareScaleDiagnostics>();
        ScaleDiagnosticsLogger.VerboseFramesEnabled = verboseFrames;

        if (scale.InputMode != ScaleInputMode.Hardware)
            await scale.SetInputModeAsync(ScaleInputMode.Hardware).ConfigureAwait(false);

        if (!scale.IsConnected)
        {
            try
            {
                await scale.PrepareAndConnectHardwareAsync(scale.HardwareSettings).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Cannot connect scale: {ex.Message}");
                return 1;
            }
        }

        var started = DateTimeOffset.UtcNow;
        var end = started.AddSeconds(seconds);
        var initialValid = scale.ValidFrameCount;
        var initialInvalid = scale.InvalidFrameCount;
        var stableCount = 0;
        var unstableCount = 0;
        var stableTransitions = 0;
        var wasStable = false;
        DateTimeOffset? lastFrameAt = null;
        var longestGap = TimeSpan.Zero;
        var intervals = new List<double>();
        long lastSequence = -1;

        while (DateTimeOffset.UtcNow < end)
        {
            var reading = scale.LatestReading;
            if (reading is not null && reading.Sequence != lastSequence)
            {
                lastSequence = reading.Sequence;
                if (lastFrameAt.HasValue)
                {
                    var gap = reading.ReceivedAt - lastFrameAt.Value;
                    intervals.Add(gap.TotalMilliseconds);
                    if (gap > longestGap)
                        longestGap = gap;
                }

                lastFrameAt = reading.ReceivedAt;

                if (reading.IsStable)
                {
                    stableCount++;
                    if (!wasStable)
                        stableTransitions++;
                    wasStable = true;
                }
                else
                {
                    unstableCount++;
                    wasStable = false;
                }
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        var valid = scale.ValidFrameCount - initialValid;
        var invalid = scale.InvalidFrameCount - initialInvalid;
        var avgInterval = intervals.Count == 0 ? 0 : intervals.Average();
        var latest = scale.LatestReading;

        Console.WriteLine($"Scale live observation: {seconds}s");
        Console.WriteLine($"Frames received (valid): {valid}");
        Console.WriteLine($"Invalid frames: {invalid}");
        Console.WriteLine($"Average frame interval: {avgInterval:F1} ms");
        Console.WriteLine($"Latest weight: {latest?.WeightKg.ToString() ?? "—"} kg");
        Console.WriteLine($"Stable frame count: {stableCount}");
        Console.WriteLine($"Unstable frame count: {unstableCount}");
        Console.WriteLine($"Stable transitions: {stableTransitions}");
        Console.WriteLine($"Stable source: {latest?.StableSource}");
        Console.WriteLine($"Raw stable flag: {latest?.RawStableFlag}");
        Console.WriteLine($"Longest frame gap: {longestGap.TotalMilliseconds:F1} ms");
        Console.WriteLine($"Is stable at end: {scale.IsStable}");

        return scale.IsConnected && !scale.IsStale && scale.IsStable ? 0 : 1;
    }
}
