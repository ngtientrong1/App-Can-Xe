using System.IO;
using CanXe.Application.Configuration;
using CanXe.Domain.Models;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase3ACameraAutoConnectDesktopTests(WpfSmokeFixture wpf)
{
    [Fact]
    public async Task AutoConnectTrue_StartupConnectsCamera()
    {
        _ = wpf;
        var camera = new ControllableCameraStreamService();
        await using var host = new CameraDesktopTestHost(camera);
        var (vm, settings) = await host.CreateMainViewModelAsync();

        settings.CameraEnabled = true;
        settings.RtspHost = "192.168.1.50";
        settings.AutoConnectCameraOnStartup = true;
        await settings.SaveCameraCommand.ExecuteAsync(null);

        await vm.InitializeAsync();
        await WaitForConditionAsync(() => camera.IsConnected, TimeSpan.FromSeconds(15));

        Assert.True(camera.IsConnected);
        Assert.Contains("Đã kết nối", vm.HeaderCameraStatus);
    }

    [Fact]
    public async Task AutoConnectFalse_StartupDoesNotConnectCamera()
    {
        _ = wpf;
        var camera = new ControllableCameraStreamService();
        await using var host = new CameraDesktopTestHost(camera);
        var (vm, settings) = await host.CreateMainViewModelAsync();

        settings.CameraEnabled = true;
        settings.RtspHost = "192.168.1.50";
        settings.AutoConnectCameraOnStartup = false;
        await settings.SaveCameraCommand.ExecuteAsync(null);

        await vm.InitializeAsync();
        await Task.Delay(500);

        Assert.Equal(0, camera.ConnectAttempts);
        Assert.False(camera.IsConnected);
    }

    [Fact]
    public async Task ManualDisconnect_DoesNotAutoReconnectInSameSession()
    {
        _ = wpf;
        var camera = new ControllableCameraStreamService();
        await using var host = new CameraDesktopTestHost(camera);
        var (vm, settings) = await host.CreateMainViewModelAsync();

        settings.CameraEnabled = true;
        settings.RtspHost = "192.168.1.50";
        settings.AutoConnectCameraOnStartup = true;
        await settings.SaveCameraCommand.ExecuteAsync(null);

        await vm.InitializeAsync();
        await WaitForConditionAsync(() => camera.IsConnected, TimeSpan.FromSeconds(15));
        var attemptsAfterConnect = camera.ConnectAttempts;

        await vm.DisconnectCameraCommand.ExecuteAsync(null);
        await Task.Delay(1500);

        Assert.Equal(attemptsAfterConnect, camera.ConnectAttempts);
        Assert.False(camera.IsConnected);
        Assert.Contains("Chưa kết nối", vm.HeaderCameraStatus);
    }

    [Fact]
    public async Task RestartAfterManualDisconnect_StillAutoConnects()
    {
        _ = wpf;
        var dbPath = Path.Combine(Path.GetTempPath(), $"canxe-desktop-cam-{Guid.NewGuid():N}.db");
        var photoRoot = Path.Combine(Path.GetTempPath(), $"canxe-desktop-cam-photos-{Guid.NewGuid():N}");

        await using (var host = new CameraDesktopTestHost(new ControllableCameraStreamService(), dbPath, photoRoot, deleteOnDispose: false))
        {
            var first = await host.CreateMainViewModelAsync();
            first.SettingsViewModel.CameraEnabled = true;
            first.SettingsViewModel.RtspHost = "192.168.1.50";
            first.SettingsViewModel.AutoConnectCameraOnStartup = true;
            await first.SettingsViewModel.SaveCameraCommand.ExecuteAsync(null);
            await first.ViewModel.InitializeAsync();
            await WaitForConditionAsync(() => first.ViewModel.CameraPreviewAvailable, TimeSpan.FromSeconds(15));
            await first.ViewModel.DisconnectCameraCommand.ExecuteAsync(null);
            await first.ViewModel.DisposeAsync();
        }

        await using (var restartHost = new CameraDesktopTestHost(new ControllableCameraStreamService(), dbPath, photoRoot))
        {
            var second = await restartHost.CreateMainViewModelAsync();
            await second.ViewModel.InitializeAsync();
            await WaitForConditionAsync(
                () => second.ViewModel.CameraPreviewAvailable
                    && second.ViewModel.HeaderCameraStatus.Contains("Đã kết nối", StringComparison.Ordinal),
                TimeSpan.FromSeconds(15));
            Assert.Contains("Đã kết nối", second.ViewModel.HeaderCameraStatus);
            await second.ViewModel.DisposeAsync();
        }
    }

    [Fact]
    public async Task PreviewDrawerToggle_DoesNotReconnect()
    {
        _ = wpf;
        var camera = new ControllableCameraStreamService();
        await using var host = new CameraDesktopTestHost(camera);
        var (vm, settings) = await host.CreateMainViewModelAsync();

        settings.CameraEnabled = true;
        settings.RtspHost = "192.168.1.50";
        settings.AutoConnectCameraOnStartup = true;
        await settings.SaveCameraCommand.ExecuteAsync(null);

        await vm.InitializeAsync();
        await WaitForConditionAsync(() => camera.IsConnected, TimeSpan.FromSeconds(15));
        var attemptsBefore = camera.ConnectAttempts;

        vm.ToggleCameraDrawerCommand.Execute(null);
        vm.ToggleCameraDrawerCommand.Execute(null);
        await Task.Delay(300);

        Assert.Equal(attemptsBefore, camera.ConnectAttempts);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }
}

internal sealed class CameraDesktopTestHost : IAsyncDisposable
{
    private readonly DesktopTestHost _inner;

    public CameraDesktopTestHost(
        ControllableCameraStreamService camera,
        string? databasePath = null,
        string? photoRootPath = null,
        bool deleteOnDispose = true) =>
        _inner = new DesktopTestHost(
            new AppSettings { DeviceMode = "Hardware" },
            camera,
            databasePath,
            photoRootPath,
            deleteOnDispose);

    public async Task<(MainViewModel ViewModel, SettingsViewModel SettingsViewModel)> CreateMainViewModelAsync()
    {
        var result = await _inner.CreateMainViewModelForBindingSmokeAsync();
        result.SettingsViewModel.AutoConnectScaleOnStartup = false;
        await result.SettingsViewModel.PersistScaleSettingsAsync(result.SettingsViewModel.SavedScaleInputMode ?? ScaleInputMode.Hardware);
        return result;
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
