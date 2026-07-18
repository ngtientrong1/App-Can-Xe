using System.IO;
using CanXe.Desktop.Services;
using CanXe.Infrastructure.Logging;

namespace CanXe.Desktop.Tests;

public sealed class Phase8AppShutdownCoordinatorTests : IDisposable
{
    private readonly string _logDir;

    public Phase8AppShutdownCoordinatorTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), "CanXeDesktopLifecycle", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_logDir);
        CanXeLogPaths.SetOverrideRoot(_logDir);
        AppShutdownCoordinator.ResetForTests();
    }

    public void Dispose()
    {
        AppShutdownCoordinator.ResetForTests();
        CanXeLogPaths.ClearOverrideRoot();
        try
        {
            if (Directory.Exists(_logDir))
                Directory.Delete(_logDir, recursive: true);
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public async Task AppShutdown_DoesNotWaitForever_RunStepTimesOut()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await AppShutdownCoordinator.RunStepAsync(
            "HangingService",
            TimeSpan.FromMilliseconds(200),
            async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            });
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
        var log = LifecycleLogger.ReadAllTextSafe();
        Assert.Contains("ShutdownTimeout", log);
        Assert.Contains("HangingService", log);
    }

    [Fact]
    public void AppShutdown_BeginShutdown_IsIdempotent()
    {
        AppShutdownCoordinator.BeginShutdown();
        AppShutdownCoordinator.BeginShutdown();
        Assert.True(AppShutdownCoordinator.IsShuttingDown);
        var log = LifecycleLogger.ReadAllTextSafe();
        Assert.Contains("ShutdownRequested", log);
    }

    [Fact]
    public void AppShutdown_StopsTimers_ThemeServiceDispose()
    {
        var theme = new ThemeService();
        theme.Load();
        var ex = Record.Exception(() => theme.Dispose());
        Assert.Null(ex);
        ex = Record.Exception(() => theme.Dispose());
        Assert.Null(ex);
    }
}
