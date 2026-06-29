using System.Diagnostics;
using CanXe.Application.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class DiagnosticsRunOrchestrator
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DiagnosticsRunOrchestrator(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory;

    public async Task<SystemDiagnosticsResult> RunAsync(
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var checks = new List<SystemDiagnosticCheck>();

        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallCts.CancelAfter(DiagnosticsTimeouts.Overall);

        if (options.PublishCheck)
        {
            await RunPhaseAsync(
                checks,
                "Publish checks",
                DiagnosticsTimeouts.Application,
                overallCts.Token,
                (runner, ct) => runner.RunPublishChecksAsync(options, ct)).ConfigureAwait(false);
        }

        if (options.IncludeApplication)
        {
            await RunPhaseAsync(
                checks,
                "Application checks",
                DiagnosticsTimeouts.Application,
                overallCts.Token,
                (runner, ct) => runner.RunApplicationChecksAsync(ct)).ConfigureAwait(false);
        }

        if (options.IncludeDatabase)
        {
            await RunPhaseAsync(
                checks,
                "Database checks",
                DiagnosticsTimeouts.Database,
                overallCts.Token,
                (runner, ct) => runner.RunDatabaseChecksAsync(ct)).ConfigureAwait(false);
        }

        if (options.IncludeFfmpeg)
        {
            await RunPhaseAsync(
                checks,
                "FFmpeg checks",
                DiagnosticsTimeouts.Ffmpeg,
                overallCts.Token,
                (runner, ct) => runner.RunFfmpegChecksAsync(options, ct)).ConfigureAwait(false);
        }

        if (options.IncludeCameraConfig || options.IncludeCameraConnection)
        {
            await RunPhaseAsync(
                checks,
                "Camera checks",
                DiagnosticsTimeouts.Camera,
                overallCts.Token,
                async (runner, ct) =>
                {
                    var cameraChecks = new List<SystemDiagnosticCheck>();
                    if (options.IncludeCameraConfig)
                        cameraChecks.AddRange(await runner.RunCameraConfigChecksAsync(options, ct).ConfigureAwait(false));
                    if (options.IncludeCameraConnection)
                        cameraChecks.AddRange(await runner.RunCameraConnectionChecksAsync(options, ct).ConfigureAwait(false));

                    await DiagnosticsCleanup.WaitForFfmpegDrainAsync(ct).ConfigureAwait(false);
                    return cameraChecks;
                }).ConfigureAwait(false);
        }

        if (options.IncludeScale)
        {
            await RunPhaseAsync(
                checks,
                "Scale checks",
                DiagnosticsTimeouts.Scale,
                overallCts.Token,
                (runner, ct) => runner.RunScaleChecksAsync(options, ct)).ConfigureAwait(false);
        }

        var result = new SystemDiagnosticsResult
        {
            Checks = checks,
            TotalDuration = Stopwatch.GetElapsedTime(started)
        };

        DiagnosticsLogger.Write(
            $"Diagnostics completed: {result.Passed} pass, {result.Failed} fail, {result.Skipped} skipped");
        return result;
    }

    private async Task RunPhaseAsync(
        List<SystemDiagnosticCheck> checks,
        string phaseName,
        TimeSpan timeout,
        CancellationToken overallToken,
        Func<DiagnosticsPhaseRunner, CancellationToken, Task<IReadOnlyList<SystemDiagnosticCheck>>> execute)
    {
        DiagnosticsProgressReporter.RunStarted(phaseName);
        using var scope = _scopeFactory.CreateScope();
        using var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken);
        phaseCts.CancelAfter(timeout);

        try
        {
            var runner = scope.ServiceProvider.GetRequiredService<DiagnosticsPhaseRunner>();
            checks.AddRange(await execute(runner, phaseCts.Token).ConfigureAwait(false));
            DiagnosticsProgressReporter.RunPassed(phaseName);
        }
        catch (OperationCanceledException) when (!overallToken.IsCancellationRequested)
        {
            var detail = $"{phaseName} timeout after {timeout.TotalSeconds:F0}s";
            DiagnosticsProgressReporter.RunFailed(phaseName, detail);
            checks.Add(PhaseTimeoutCheck(phaseName, detail));
        }
        catch (Exception ex)
        {
            DiagnosticsProgressReporter.RunFailed(phaseName, ex.Message);
            checks.Add(PhaseTimeoutCheck(phaseName, ex.Message));
        }
    }

    private static SystemDiagnosticCheck PhaseTimeoutCheck(string phaseName, string detail)
    {
        var category = phaseName switch
        {
            "Application checks" => "Application",
            "Database checks" => "Database",
            "FFmpeg checks" => "FFmpeg",
            "Camera checks" => "Camera",
            "Scale checks" => "Scale",
            "Publish checks" => "Publish",
            _ => "Diagnostics"
        };

        return new SystemDiagnosticCheck
        {
            Category = category,
            Name = "Phase timeout",
            Status = DiagnosticStatus.Fail,
            Detail = detail
        };
    }
}
