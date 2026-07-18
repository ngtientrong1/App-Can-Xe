using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Scale;
using CanXe.ScaleProtocol.Core;
using CanXe.Tests.Support;

namespace CanXe.Tests.Application;

public sealed class Phase8ScaleWatchdogTests
{
    [Fact]
    public void ScaleWatchdog_PortOpenButNoData_TriggersAutoReconnect()
    {
        var now = DateTimeOffset.Parse("2026-07-18T10:00:00+07:00");
        var waitingSince = now.AddSeconds(-7);

        var should = ScaleWatchdogPolicy.ShouldTriggerNoDataTimeout(
            isShuttingDown: false,
            disconnectedByUser: false,
            isHardwareMode: true,
            isReconnectInProgress: false,
            portOpen: true,
            isDataAlive: false,
            waitingForDataSince: waitingSince,
            now: now);

        Assert.True(should);
    }

    [Fact]
    public void ScaleWatchdog_ValidFrame_ResetsRetryCount()
    {
        var now = DateTimeOffset.UtcNow;
        var alive = ScaleWatchdogPolicy.IsDataAlive(now.AddSeconds(-1), now);
        Assert.True(alive);

        var should = ScaleWatchdogPolicy.ShouldTriggerNoDataTimeout(
            isShuttingDown: false,
            disconnectedByUser: false,
            isHardwareMode: true,
            isReconnectInProgress: false,
            portOpen: true,
            isDataAlive: alive,
            waitingForDataSince: now.AddSeconds(-30),
            now: now);
        Assert.False(should);
        Assert.Equal(TimeSpan.Zero, ScaleWatchdogPolicy.GetReconnectBackoff(0));
    }

    [Fact]
    public void ScaleWatchdog_DoesNotReconnectWhileDataAlive()
    {
        Assert.True(ScaleWatchdogPolicy.ShouldReconnectWhileDataAlive(isDataAlive: true, isShuttingDown: false));

        var should = ScaleWatchdogPolicy.ShouldTriggerNoDataTimeout(
            isShuttingDown: false,
            disconnectedByUser: false,
            isHardwareMode: true,
            isReconnectInProgress: false,
            portOpen: true,
            isDataAlive: true,
            waitingForDataSince: DateTimeOffset.UtcNow.AddSeconds(-60),
            now: DateTimeOffset.UtcNow);

        Assert.False(should);
    }

    [Fact]
    public void ScaleWatchdog_DoesNotReconnectDuringShutdown()
    {
        var now = DateTimeOffset.UtcNow;
        var should = ScaleWatchdogPolicy.ShouldTriggerNoDataTimeout(
            isShuttingDown: true,
            disconnectedByUser: false,
            isHardwareMode: true,
            isReconnectInProgress: false,
            portOpen: true,
            isDataAlive: false,
            waitingForDataSince: now.AddSeconds(-30),
            now: now);

        Assert.False(should);
    }

    [Fact]
    public async Task ScaleReconnect_DoesNotRunConcurrently()
    {
        var gate = new ScaleReconnectGate();
        Assert.True(gate.TryEnter());
        Assert.True(gate.IsInProgress);
        Assert.False(gate.TryEnter());

        var secondEntered = false;
        var t = Task.Run(() =>
        {
            if (gate.TryEnter())
            {
                secondEntered = true;
                gate.Exit();
            }
        });

        await Task.Delay(50);
        Assert.False(secondEntered);
        gate.Exit();
        await t;
        Assert.True(gate.TryEnter());
        gate.Exit();
    }

    [Fact]
    public void ScaleReconnect_ManualButtonUsesSameFlow()
    {
        Assert.Contains("chờ dữ liệu", ScaleWatchdogPolicy.FormatWaitingForDataStatus("COM1", 1200), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Đã kết nối COM1 @ 1200", ScaleWatchdogPolicy.FormatConnectedStatus("COM1", 1200));
        Assert.Equal(TimeSpan.FromSeconds(2), ScaleWatchdogPolicy.GetReconnectBackoff(1));
        Assert.Equal(TimeSpan.FromSeconds(5), ScaleWatchdogPolicy.GetReconnectBackoff(2));
        Assert.Equal(TimeSpan.FromSeconds(10), ScaleWatchdogPolicy.GetReconnectBackoff(3));
        Assert.Equal(TimeSpan.FromSeconds(30), ScaleWatchdogPolicy.GetReconnectBackoff(4));
        Assert.Equal(TimeSpan.FromSeconds(30), ScaleWatchdogPolicy.GetReconnectBackoff(99));
    }

    [Fact]
    public async Task ScaleReconnect_ComBusySoftFailsWithoutUiFreeze()
    {
        var reader = new FakeScaleSerialReader { ConnectShouldFail = true, ConnectDelay = TimeSpan.FromMilliseconds(30) };
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ex = await Record.ExceptionAsync(async () =>
            await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM9" })
                .WaitAsync(TimeSpan.FromSeconds(3)));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
        Assert.NotNull(ex);
        Assert.False(service.IsConnected);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task Shutdown_ProcessStillExitsCleanlyAfterWatchdog()
    {
        var reader = new FakeScaleSerialReader { DisconnectDelay = TimeSpan.FromSeconds(20) };
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.ConnectHardwareAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await service.DisposeAsync();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
        Assert.False(reader.IsPortOpen);
    }

    [Fact]
    public void IsDataAlive_UsesLastValidFrameWindow()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.False(ScaleWatchdogPolicy.IsDataAlive(null, now));
        Assert.False(ScaleWatchdogPolicy.IsDataAlive(now.AddSeconds(-10), now));
        Assert.True(ScaleWatchdogPolicy.IsDataAlive(now.AddSeconds(-1), now));
    }
}
