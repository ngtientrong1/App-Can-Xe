using System.IO;
using CanXe.Desktop.Services;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Tests;

public sealed class Phase8ScaleReconnectDesktopTests : IDisposable
{
    public Phase8ScaleReconnectDesktopTests()
    {
        AppShutdownCoordinator.ResetForTests();
    }

    public void Dispose() => AppShutdownCoordinator.ResetForTests();

    [Fact]
    public void Startup_ShowsUiBeforeComConnectCompletes()
    {
        var appPath = Path.Combine(FindRepoRoot(), "src", "CanXe.Desktop", "App.xaml.cs");
        var text = File.ReadAllText(appPath);
        var showIdx = text.IndexOf("mainWindow.Show()", StringComparison.Ordinal);
        var initIdx = text.IndexOf("await viewModel.InitializeAsync()", StringComparison.Ordinal);
        Assert.True(showIdx > 0 && initIdx > showIdx, "UI Show() must precede InitializeAsync()");
    }

    [Fact]
    public void Shutdown_StopsScaleWatchdog()
    {
        AppShutdownCoordinator.BeginShutdown();
        Assert.True(AppShutdownCoordinator.IsShuttingDown);

        var should = ScaleWatchdogPolicy.ShouldTriggerNoDataTimeout(
            isShuttingDown: AppShutdownCoordinator.IsShuttingDown,
            disconnectedByUser: false,
            isHardwareMode: true,
            isReconnectInProgress: false,
            portOpen: true,
            isDataAlive: false,
            waitingForDataSince: DateTimeOffset.UtcNow.AddSeconds(-30),
            now: DateTimeOffset.UtcNow);
        Assert.False(should);
    }

    [Fact]
    public void ScaleReconnect_ManualButtonUsesSameFlow_Source()
    {
        var autoConnect = Path.Combine(FindRepoRoot(), "src", "CanXe.Desktop", "ViewModels", "MainViewModel.AutoConnect.cs");
        var text = File.ReadAllText(autoConnect);
        Assert.Contains("ReconnectAsync(\"ManualRetry\")", text, StringComparison.Ordinal);
        Assert.Contains("ReconnectAsync(\"NoDataTimeout\")", text, StringComparison.Ordinal);
        Assert.Contains("ScaleReconnectGate", text, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CanXe.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
