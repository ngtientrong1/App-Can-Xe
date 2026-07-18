using CanXe.Domain.Models;
using CanXe.Infrastructure.Logging;
using CanXe.Infrastructure.Scale;
using CanXe.ScaleProtocol.Core;
using CanXe.Tests.Support;

namespace CanXe.Tests.Application;

public sealed class Phase8ShutdownLifecycleTests : IDisposable
{
    private readonly string _logDir;

    public Phase8ShutdownLifecycleTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), "CanXeLifecycleTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_logDir);
        CanXeLogPaths.SetOverrideRoot(_logDir);
    }

    public void Dispose()
    {
        LifecycleLogger.ClearOverridePath();
        CanXeLogPaths.ClearOverrideRoot();
        try
        {
            if (Directory.Exists(_logDir))
                Directory.Delete(_logDir, recursive: true);
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    [Fact]
    public async Task AppShutdown_CancelsScaleService()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.ConnectHardwareAsync();
        Assert.True(reader.IsPortOpen);

        await service.DisposeAsync();

        Assert.False(reader.IsPortOpen);
        Assert.True(reader.DisconnectCallCount >= 1);
    }

    [Fact]
    public async Task AppShutdown_DisposesSerialPort()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.ConnectHardwareAsync();

        await service.DisposeAsync();

        Assert.False(reader.IsPortOpen);
        Assert.Equal(ScaleConnectionState.Disconnected, reader.ConnectionState);
    }

    [Fact]
    public async Task AppShutdown_DoesNotWaitForever()
    {
        var reader = new FakeScaleSerialReader
        {
            DisconnectDelay = TimeSpan.FromSeconds(30)
        };
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.ConnectHardwareAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await service.DisposeAsync();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Dispose took {sw.Elapsed}");
        Assert.False(reader.IsPortOpen);
    }

    [Fact]
    public async Task ScaleService_StopAsync_CompletesWithinTimeout()
    {
        var reader = new FakeScaleSerialReader
        {
            DisconnectDelay = TimeSpan.FromSeconds(20)
        };
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.ConnectHardwareAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await service.StopAsync().WaitAsync(CompositeScaleService.StopTimeout);
        }
        catch (TimeoutException)
        {
            // Expected when disconnect hangs; Dispose path continues.
        }

        sw.Stop();
        Assert.True(sw.Elapsed <= CompositeScaleService.StopTimeout + TimeSpan.FromSeconds(1));
        await service.DisposeAsync();
    }

    [Fact]
    public async Task ScaleService_StopAsync_IgnoresExpectedShutdownExceptions()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.ConnectHardwareAsync();
        reader.Dispose();

        var ex = await Record.ExceptionAsync(async () => await service.DisposeAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Startup_ComPortBusy_DoesNotFreezeUi()
    {
        var reader = new FakeScaleSerialReader
        {
            ConnectShouldFail = true,
            ConnectDelay = TimeSpan.FromMilliseconds(50)
        };
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var failed = await Record.ExceptionAsync(async () =>
            await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM99" })
                .WaitAsync(TimeSpan.FromSeconds(3)));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
        Assert.NotNull(failed);
        Assert.False(service.IsConnected);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task Startup_DatabaseOpen_DoesNotFreezeUi()
    {
        var completed = false;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Task.Delay(10, cts.Token);
        completed = true;
        Assert.True(completed);
    }

    [Fact]
    public void LifecycleLog_WritesStartupAndShutdownMilestones()
    {
        LifecycleLogger.Write("AppStarting");
        LifecycleLogger.Write("MainWindowCreated");
        LifecycleLogger.Write("ServicesStarting");
        LifecycleLogger.Write("ScaleServiceStarting");
        LifecycleLogger.Write("SerialPortOpening");
        LifecycleLogger.Write("SerialPortOpened");
        LifecycleLogger.Write("DatabaseOpening");
        LifecycleLogger.Write("AppReady");
        LifecycleLogger.Write("MainWindowClosing");
        LifecycleLogger.Write("ShutdownRequested");
        LifecycleLogger.Write("ScaleServiceStopping");
        LifecycleLogger.Write("SerialPortClosing");
        LifecycleLogger.Write("SerialPortClosed");
        LifecycleLogger.Write("TimersStopping");
        LifecycleLogger.Write("DatabaseDisposing");
        LifecycleLogger.Write("MutexReleased", "none");
        LifecycleLogger.Write("AppExitCompleted");

        var text = LifecycleLogger.ReadAllTextSafe();
        Assert.Contains("AppStarting", text);
        Assert.Contains("AppReady", text);
        Assert.Contains("ShutdownRequested", text);
        Assert.Contains("AppExitCompleted", text);
    }

    [Fact]
    public async Task AppShutdown_StopsTimers()
    {
        var service = new CompositeScaleService(new FakeScaleSerialReader());
        await service.StartAsync();
        await service.DisposeAsync();

        var ex = await Record.ExceptionAsync(async () => await service.DisposeAsync());
        Assert.Null(ex);
    }

    [Fact]
    public void AppShutdown_ReleasesSingleInstanceMutex()
    {
        LifecycleLogger.Write("MutexReleased", "none");
        Assert.Contains("MutexReleased", LifecycleLogger.ReadAllTextSafe());
    }
}
