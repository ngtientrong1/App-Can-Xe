using CanXe.Desktop.ViewModels;
using CanXe.Infrastructure.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace CanXe.Desktop.Services;

/// <summary>
/// Coordinates timed app shutdown so UI close always exits the process.
/// Individual steps are capped; overall budget is 3–5 seconds.
/// </summary>
public static class AppShutdownCoordinator
{
    public static bool IsShuttingDown { get; private set; }

    public static TimeSpan OverallTimeout { get; } = TimeSpan.FromSeconds(4);
    public static TimeSpan ServiceStopTimeout { get; } = TimeSpan.FromSeconds(2);

    public static void BeginShutdown()
    {
        if (IsShuttingDown)
            return;

        IsShuttingDown = true;
        LifecycleLogger.Write("ShutdownRequested");
    }

    public static void ResetForTests()
    {
        IsShuttingDown = false;
    }

    public static async Task RunAsync(
        MainViewModel? viewModel,
        ThemeService? themeService,
        IHost? host,
        CancellationToken cancellationToken = default)
    {
        BeginShutdown();

        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallCts.CancelAfter(OverallTimeout);
        var overall = overallCts.Token;

        viewModel?.BeginShutdown();

        await RunStepAsync(
            "ScaleServiceStopping",
            ServiceStopTimeout,
            async ct =>
            {
                if (viewModel is null)
                    return;
                await viewModel.DisposeAsync().AsTask().WaitAsync(ct).ConfigureAwait(false);
            },
            overall).ConfigureAwait(false);

        LifecycleLogger.Write("TimersStopping");
        try
        {
            themeService?.Dispose();
        }
        catch (Exception ex)
        {
            LifecycleLogger.Write("TimersStopping", ex.GetType().Name);
        }

        LifecycleLogger.Write("DatabaseDisposing");
        try
        {
            SqliteConnection.ClearAllPools();
        }
        catch (Exception ex)
        {
            LifecycleLogger.Write("DatabaseDisposing", ex.GetType().Name);
        }

        await RunStepAsync(
            "HostStopping",
            ServiceStopTimeout,
            async ct =>
            {
                if (host is null)
                    return;
                await host.StopAsync(ct).ConfigureAwait(false);
            },
            overall).ConfigureAwait(false);

        try
        {
            host?.Dispose();
        }
        catch (Exception ex)
        {
            LifecycleLogger.Write("HostDispose", ex.GetType().Name);
        }

        // No single-instance mutex in this build.
        LifecycleLogger.Write("MutexReleased", "none");
        LifecycleLogger.Write("AppExitCompleted");
    }

    public static async Task RunStepAsync(
        string serviceName,
        TimeSpan stepTimeout,
        Func<CancellationToken, Task> action,
        CancellationToken overallToken = default)
    {
        try
        {
            using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken);
            stepCts.CancelAfter(stepTimeout);
            await action(stepCts.Token).ConfigureAwait(false);
            LifecycleLogger.Write(serviceName, "ok");
        }
        catch (OperationCanceledException)
        {
            LifecycleLogger.Write("ShutdownTimeout", serviceName);
        }
        catch (TimeoutException)
        {
            LifecycleLogger.Write("ShutdownTimeout", serviceName);
        }
        catch (Exception ex)
        {
            LifecycleLogger.Write(serviceName, $"error:{ex.GetType().Name}");
        }
    }
}
