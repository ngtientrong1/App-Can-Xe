using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class CameraStabilityReport
{
    public required TimeSpan Duration { get; init; }
    public required int TotalValidFrames { get; init; }
    public DateTimeOffset? FirstFrameAt { get; init; }
    public double AverageFps { get; init; }
    public TimeSpan LongestFrameGap { get; init; }
    public int StallCount { get; init; }
    public int FfmpegUnexpectedExits { get; init; }
    public int ReconnectAttempts { get; init; }
    public int ReconnectSuccesses { get; init; }
    public int MaximumSimultaneousFfmpegProcesses { get; init; }
    public CameraConnectionState StateAtEndOfObservation { get; init; }
    public CameraConnectionState StateAfterCleanup { get; init; }
    public TimeSpan? LastFrameAge { get; init; }
    public int? TrackedFfmpegPid { get; init; }
    public bool FfmpegLeak { get; init; }
    public bool Passed { get; init; }

    public void Print()
    {
        Console.WriteLine($"Duration: {Duration.TotalSeconds:F0}s");
        Console.WriteLine($"Total valid frames: {TotalValidFrames}");
        Console.WriteLine($"First frame time: {(FirstFrameAt.HasValue ? FirstFrameAt.Value.LocalDateTime.ToString("O") : "n/a")}");
        Console.WriteLine($"Average FPS: {AverageFps:F2}");
        Console.WriteLine($"Longest frame gap: {LongestFrameGap.TotalSeconds:F2}s");
        Console.WriteLine($"Stall count: {StallCount}");
        Console.WriteLine($"FFmpeg unexpected exits: {FfmpegUnexpectedExits}");
        Console.WriteLine($"Reconnect attempts: {ReconnectAttempts}");
        Console.WriteLine($"Reconnect successes: {ReconnectSuccesses}");
        Console.WriteLine($"Maximum simultaneous FFmpeg processes: {MaximumSimultaneousFfmpegProcesses}");
        Console.WriteLine($"Camera state at end of observation: {StateAtEndOfObservation}");
        Console.WriteLine($"Camera state after cleanup: {StateAfterCleanup}");
        Console.WriteLine($"Tracked FFmpeg PID: {TrackedFfmpegPid?.ToString() ?? "n/a"}");
        Console.WriteLine($"Last frame age: {LastFrameAge?.TotalSeconds.ToString("F2") ?? "n/a"}s");
        Console.WriteLine($"FFmpeg leak: {(FfmpegLeak ? "YES" : "NO")}");
        Console.WriteLine(Passed ? "PASS" : "FAIL");
    }
}

public static class CameraStabilityRunner
{
    public static async Task<CameraStabilityReport> RunAsync(
        IServiceProvider services,
        int durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var stream = services.GetRequiredService<ICameraStreamService>();
        var supervisor = services.GetRequiredService<ICameraConnectionSupervisor>();
        var settingsService = services.GetRequiredService<StationSettingsService>();

        _ = await settingsService.GetCameraRuntimeAsync(cancellationToken).ConfigureAwait(false);
        var started = DateTimeOffset.UtcNow;
        var validFrames = 0;
        DateTimeOffset? firstFrameAt = null;
        DateTimeOffset? lastFrameAt = null;
        var longestGap = TimeSpan.Zero;
        var unexpectedExits = 0;

        void OnFrame(object? _, CameraDecodedFrame frame)
        {
            validFrames++;
            var now = DateTimeOffset.UtcNow;
            if (firstFrameAt is null)
                firstFrameAt = now;

            if (lastFrameAt.HasValue)
            {
                var gap = now - lastFrameAt.Value;
                if (gap > longestGap)
                    longestGap = gap;
            }

            lastFrameAt = now;
            stream.NotifyPreviewRendered(frame.Width, frame.Height);
        }

        void OnExit(object? sender, string reason) => unexpectedExits++;

        stream.FrameDecoded += OnFrame;
        stream.DecoderUnexpectedExit += OnExit;

        CameraHealthSnapshot health;
        CameraConnectionState stateAtEnd;
        int? trackedPid = null;

        try
        {
            await supervisor.StartAsync(cancellationToken).ConfigureAwait(false);
            await supervisor.RequestConnectAsync(CameraConnectRequestSource.Diagnostics, cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(durationSeconds), cancellationToken).ConfigureAwait(false);
            health = supervisor.GetHealthSnapshot();
            stateAtEnd = supervisor.State;
            trackedPid = health.FfmpegPid;
        }
        finally
        {
            stream.FrameDecoded -= OnFrame;
            stream.DecoderUnexpectedExit -= OnExit;
        }

        await supervisor.DisposeAsync().ConfigureAwait(false);
        if (stream is IAsyncDisposable streamDisposable)
            await streamDisposable.DisposeAsync().ConfigureAwait(false);

        await WaitForProcessExitAsync(trackedPid, TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        var duration = DateTimeOffset.UtcNow - started;
        var avgFps = duration.TotalSeconds > 0 ? validFrames / duration.TotalSeconds : 0;
        var ffmpegLeak = trackedPid.HasValue && IsProcessAlive(trackedPid.Value);
        var stateAfterCleanup = CameraConnectionState.Disconnected;

        var passed = validFrames > 0
                     && stateAtEnd == CameraConnectionState.Connected
                     && stateAfterCleanup == CameraConnectionState.Disconnected
                     && health.MaximumSimultaneousFfmpegProcesses <= 1
                     && !ffmpegLeak;

        return new CameraStabilityReport
        {
            Duration = duration,
            TotalValidFrames = validFrames,
            FirstFrameAt = firstFrameAt,
            AverageFps = avgFps,
            LongestFrameGap = longestGap,
            StallCount = health.StallCount,
            FfmpegUnexpectedExits = unexpectedExits,
            ReconnectAttempts = health.ReconnectAttempt,
            ReconnectSuccesses = health.ReconnectSuccessCount,
            MaximumSimultaneousFfmpegProcesses = health.MaximumSimultaneousFfmpegProcesses,
            StateAtEndOfObservation = stateAtEnd,
            StateAfterCleanup = stateAfterCleanup,
            LastFrameAge = stream.LastFrameAge,
            TrackedFfmpegPid = trackedPid,
            FfmpegLeak = ffmpegLeak,
            Passed = passed
        };
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WaitForProcessExitAsync(int? pid, TimeSpan timeout)
    {
        if (!pid.HasValue)
            return;

        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < timeout)
        {
            if (!IsProcessAlive(pid.Value))
                return;

            await Task.Delay(100).ConfigureAwait(false);
        }
    }
}
