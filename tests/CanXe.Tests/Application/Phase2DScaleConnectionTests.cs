using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Scale;
using CanXe.ScaleProtocol.Core;
using CanXe.Tests.Support;

namespace CanXe.Tests.Application;

public class Phase2DScaleConnectionTests
{
    [Fact]
    public async Task ReconfigureAndConnect_ClosesPortBeforeUpdateSettings()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        var first = new ScaleSerialSettings { PortName = "COM1", BaudRate = 1200 };
        await service.PrepareAndConnectHardwareAsync(first);
        Assert.True(service.IsConnected);

        var second = new ScaleSerialSettings { PortName = "COM1", BaudRate = 9600 };
        await service.PrepareAndConnectHardwareAsync(second);

        Assert.Equal(2, reader.ReconfigureAndConnectCallCount);
        Assert.Equal(9600, reader.LastAppliedSettings.BaudRate);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task UpdateSettingsWhilePortOpen_Throws()
    {
        var reader = new FakeScaleSerialReader();
        await reader.ConnectAsync();

        Assert.Throws<InvalidOperationException>(() =>
            reader.UpdateSettings(new ScaleSerialSettings { PortName = "COM2" }));
    }

    [Fact]
    public async Task ConcurrentPrepareAndConnect_OnlyOneConnectExecutesAtATime()
    {
        var reader = new FakeScaleSerialReader { ConnectDelay = TimeSpan.FromMilliseconds(200) };
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        var settings = new ScaleSerialSettings { PortName = "COM1", BaudRate = 1200 };
        var first = service.PrepareAndConnectHardwareAsync(settings);
        await Task.Delay(20);
        var second = service.PrepareAndConnectHardwareAsync(settings);

        await Task.WhenAll(first, second);

        Assert.Equal(1, reader.MaxConcurrentConnectAttempts);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task SuccessfulConnect_DoesNotThrowUpdateSettingsWhileOpen()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM1", BaudRate = 1200 });

        var ex = Record.Exception(() =>
            reader.UpdateSettings(new ScaleSerialSettings { PortName = "COM2" }));
        Assert.IsType<InvalidOperationException>(ex);
    }

    [Fact]
    public async Task SwitchManualToHardware_ReconnectsOnce()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM1", BaudRate = 1200 });

        await service.SetInputModeAsync(ScaleInputMode.SimulationManual);
        Assert.False(service.IsConnected);

        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM1", BaudRate = 1200 });

        Assert.Equal(2, reader.ReconfigureAndConnectCallCount);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task SaveStyleReconnect_DisconnectThenReconfigure()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM1", BaudRate = 1200 });

        await service.DisconnectHardwareAsync();
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM1", BaudRate = 1200 });

        Assert.Equal(2, reader.ReconfigureAndConnectCallCount);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public void AutoConnectPolicy_RequiresHardwareMode()
    {
        Assert.False(ScaleAutoConnectPolicy.ShouldAutoConnectOnStartup(
            "Hardware",
            ScaleInputMode.SimulationManual.ToString(),
            true));
    }
}
