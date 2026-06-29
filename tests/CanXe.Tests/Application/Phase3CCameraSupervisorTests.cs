using CanXe.Application.Configuration;
using CanXe.Application.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public sealed class Phase3CCameraSupervisorTests
{
    private static CameraSupervisorOptions FastOptions => new()
    {
        StartupFirstFrameTimeoutSeconds = 4,
        FrameStallWarningSeconds = 5,
        FrameStallRestartSeconds = 12,
        ReconnectInitialDelaySeconds = 1,
        ReconnectMaxDelaySeconds = 2,
        ReconnectFailureThreshold = 5,
        WatchdogIntervalSeconds = 1
    };

    [Fact]
    public async Task NormalFrames_ReachConnected()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));
        Assert.Equal(CameraConnectionState.Connected, supervisor.State);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task FramePause3Seconds_RemainsConnected()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));

        stream.SetEmitFrames(false);
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.Equal(CameraConnectionState.Connected, supervisor.State);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task FramePauseOver5Seconds_BecomesStalled()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));

        stream.SetEmitFrames(false);
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Stalled, TimeSpan.FromSeconds(8));
        Assert.Equal(CameraConnectionState.Stalled, supervisor.State);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task FrameResumesBeforeRestart_ReturnsConnectedWithoutRestart()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));

        var attemptsBefore = stream.ConnectAttempts;
        stream.SetEmitFrames(false);
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Stalled, TimeSpan.FromSeconds(8));
        stream.SetEmitFrames(true);
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(6));
        Assert.Equal(attemptsBefore, stream.ConnectAttempts);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task ProcessAliveNoFrameOver12Seconds_RestartsDecoder()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));

        var attemptsBefore = stream.ConnectAttempts;
        stream.SetEmitFrames(false);
        await WaitUntilAsync(() => stream.ConnectAttempts > attemptsBefore, TimeSpan.FromSeconds(15));
        Assert.True(stream.ConnectAttempts > attemptsBefore);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task FfmpegExit_TriggersReconnect()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));

        var attemptsBefore = stream.ConnectAttempts;
        stream.SimulateProcessExit();
        await WaitUntilAsync(() => stream.ConnectAttempts > attemptsBefore, TimeSpan.FromSeconds(8));
        Assert.True(stream.ConnectAttempts > attemptsBefore);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task StartupAutoConnect_Works()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream, hardwareMode: true, autoConnect: true);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));
        Assert.Equal(CameraConnectionState.Connected, supervisor.State);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task StartupConnectFail_RetriesAutomatically()
    {
        var stream = new ControllableCameraStreamService();
        stream.Configure(failuresBeforeSuccess: 1);
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => stream.ConnectAttempts >= 2, TimeSpan.FromSeconds(10));
        Assert.True(stream.ConnectAttempts >= 2);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task ManualConnectDuringReconnect_DoesNotCreateSecondProcess()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));

        stream.SetEmitFrames(false);
        var reconnect = supervisor.RequestReconnectAsync("test");
        var manual = supervisor.RequestConnectAsync(CameraConnectRequestSource.Manual);
        await Task.WhenAll(reconnect, manual);
        Assert.Equal(1, stream.MaxConcurrentConnectOperations);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task StopApp_CancelsReconnectLoop()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        stream.SetEmitFrames(false);
        await supervisor.DisposeAsync();
        await Task.Delay(1500);
        Assert.True(supervisor.State is CameraConnectionState.Stopping or CameraConnectionState.Disconnected);
    }

    [Fact]
    public void LastValidFrameAt_UpdatesOnlyAfterPreviewRendered()
    {
        var stream = new ControllableCameraStreamService();
        Assert.Null(stream.LastValidFrameAt);
        stream.ConnectAsync(TestRuntime()).GetAwaiter().GetResult();
        Assert.NotNull(stream.LastValidFrameAt);
    }

    [Fact]
    public async Task HealthSnapshot_ReportsMaximumSimultaneousFfmpegAsOne()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = CreateSupervisor(stream);
        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));
        var health = supervisor.GetHealthSnapshot();
        Assert.Equal(1, health.MaximumSimultaneousFfmpegProcesses);
        await supervisor.DisposeAsync();
    }

    private static CameraConnectionSupervisor CreateSupervisor(
        ControllableCameraStreamService stream,
        bool hardwareMode = true,
        bool autoConnect = true)
    {
        var settings = new AppSettings { DeviceMode = hardwareMode ? "Hardware" : "Simulation" };
        var runtime = TestRuntime(autoConnect);
        return new CameraConnectionSupervisor(
            stream,
            settings,
            new NullServiceScopeFactory(),
            FastOptions,
            _ => Task.FromResult(runtime));
    }

    private static CameraRuntimeSettings TestRuntime(bool autoConnect = true) => new()
    {
        IsEnabled = true,
        AutoConnectCameraOnStartup = autoConnect,
        RtspHost = "192.168.1.10",
        RtspPort = 554,
        RtspPath = "/stream"
    };

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < timeout)
        {
            if (predicate())
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException("Condition not met within timeout.");
    }

    private sealed class NullServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new NullScope();
    }

    private sealed class NullScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new NullProvider();
        public void Dispose()
        {
        }
    }

    private sealed class NullProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
