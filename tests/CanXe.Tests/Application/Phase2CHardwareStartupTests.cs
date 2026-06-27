using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Scale;
using CanXe.ScaleProtocol.Core;
using CanXe.Tests.Support;

namespace CanXe.Tests.Application;

public class Phase2CHardwareStartupTests
{
    [Fact]
    public void HardwareDeviceMode_DefaultMode_IsHardware()
    {
        var mode = ScaleInputModeDisplay.GetDefaultMode("Hardware");
        Assert.Equal(ScaleInputMode.Hardware, mode);
    }

    [Fact]
    public void SimulationDeviceMode_DefaultMode_IsSimulationAutomatic()
    {
        var mode = ScaleInputModeDisplay.GetDefaultMode("Simulation");
        Assert.Equal(ScaleInputMode.SimulationAutomatic, mode);
    }

    [Theory]
    [InlineData("Hardware", null, ScaleInputMode.Hardware)]
    [InlineData("Hardware", ScaleInputMode.Hardware, ScaleInputMode.Hardware)]
    [InlineData("Hardware", ScaleInputMode.SimulationAutomatic, ScaleInputMode.Hardware)]
    [InlineData("Hardware", ScaleInputMode.SimulationManual, ScaleInputMode.Hardware)]
    [InlineData("Simulation", null, ScaleInputMode.SimulationAutomatic)]
    [InlineData("Simulation", ScaleInputMode.Hardware, ScaleInputMode.SimulationAutomatic)]
    [InlineData("Simulation", ScaleInputMode.SimulationManual, ScaleInputMode.SimulationManual)]
    public void ResolveStartupMode_FollowsDeviceModeRules(
        string deviceMode,
        ScaleInputMode? saved,
        ScaleInputMode expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.ResolveStartupMode(deviceMode, saved));
    }

    [Fact]
    public async Task HardwareMode_DoesNotEmitSimulatedWeightChanges()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);

        decimal? changed = null;
        service.WeightChanged += (_, w) => changed = w;

        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await Task.Delay(700);

        Assert.Null(changed);
    }

    [Fact]
    public async Task SwitchSimulationToHardware_StopsSimulation()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.SimulationAutomatic);
        _ = await service.GetCurrentWeightAsync();

        decimal? afterSwitch = null;
        service.WeightChanged += (_, w) => afterSwitch = w;

        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await Task.Delay(700);

        Assert.Null(afterSwitch);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCurrentWeightAsync());
    }

    [Fact]
    public async Task SwitchHardwareToSimulation_DisconnectsCom()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM1", BaudRate = 1200 });
        Assert.True(service.IsConnected);

        await service.SetInputModeAsync(ScaleInputMode.SimulationAutomatic);

        Assert.Equal(ScaleConnectionState.Disconnected, reader.ConnectionState);
        Assert.True(reader.DisconnectCallCount >= 1);
    }

    [Fact]
    public async Task PrepareAndConnect_AppliesSettingsBeforeOpen()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        var settings = new ScaleSerialSettings
        {
            PortName = "COM1",
            BaudRate = 1200,
            DataBits = 8,
            Parity = "None",
            StopBits = "One",
            Handshake = "None"
        };

        await service.PrepareAndConnectHardwareAsync(settings);

        Assert.Equal(1, reader.ReconfigureAndConnectCallCount);
        Assert.Equal(1, reader.ConnectCallCount);
        Assert.Equal("COM1", reader.LastAppliedSettings.PortName);
        Assert.Equal(1200, reader.LastAppliedSettings.BaudRate);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task UpdateSettingsWhileConnected_Throws()
    {
        var reader = new FakeScaleSerialReader();
        await reader.ConnectAsync();

        Assert.Throws<InvalidOperationException>(() =>
            reader.UpdateSettings(new ScaleSerialSettings { PortName = "COM2" }));
    }

    [Fact]
    public async Task ConnectFailure_DoesNotReportConnected()
    {
        var reader = new FakeScaleSerialReader { ConnectShouldFail = true };
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings { PortName = "COM1" }));

        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task HardwareDisconnected_GetCurrentWeightThrows()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCurrentWeightAsync());
    }

    [Fact]
    public async Task HardwareStale_BlocksCapture()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings());

        reader.ConfigureStableReading(50, ScaleFrameFixtures.Frame50Kg);
        reader.SetStale(true);

        Assert.False(service.CanCaptureWeight());
        Assert.Contains("cũ", service.GetHardwareCaptureBlockReason(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HardwareUnstable_BlocksCapture()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings());

        reader.PublishReading(new ScaleReading
        {
            WeightKg = 50,
            RawFrame = ScaleFrameFixtures.Frame50Kg,
            IsStable = false,
            ReceivedAt = DateTimeOffset.Now
        });

        Assert.False(service.CanCaptureWeight());
        Assert.Contains("ổn định", service.GetHardwareCaptureBlockReason(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HardwareStable_AllowsCapture()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings());

        reader.ConfigureStableReading(50, ScaleFrameFixtures.Frame50Kg);
        reader.PublishReading(reader.LatestReading!);

        Assert.True(service.CanCaptureWeight());
        Assert.Null(service.GetHardwareCaptureBlockReason());
        var weight = await service.GetCurrentWeightAsync();
        Assert.Equal(50m, weight);
    }

    [Theory]
    [InlineData(ScaleInputMode.Hardware, false, "● Đầu cân: COM")]
    [InlineData(ScaleInputMode.Hardware, true, "● Đầu cân: Mất kết nối")]
    public void HeaderBadge_ReflectsHardwareSource(ScaleInputMode mode, bool disconnected, string expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.GetHeaderScaleBadge(mode, disconnected));
    }

    [Fact]
    public void HardwareWeightSourceText_IsComSource()
    {
        Assert.Equal("Nguồn: Đầu cân COM", ScaleInputModeDisplay.GetWeightSourceText(ScaleInputMode.Hardware));
    }

    [Fact]
    public void ScaleSourceSelection_IsAlwaysAvailablePolicy()
    {
        Assert.True(ScaleInputModeDisplay.IsValidModeForDevice("Hardware", ScaleInputMode.Hardware));
        Assert.False(ScaleInputModeDisplay.IsValidModeForDevice("Simulation", ScaleInputMode.Hardware));
        Assert.False(ScaleInputModeDisplay.IsValidModeForDevice("Hardware", ScaleInputMode.SimulationManual));
        Assert.True(ScaleInputModeDisplay.IsValidModeForDevice("Hardware", ScaleInputMode.SimulationManual, developerModeEnabled: true));
    }

    [Fact]
    public void ScaleInputMode_PersistsForAllModes_OnSimulationDeployment()
    {
        Assert.True(ScaleInputModeDisplay.ShouldPersistMode("Simulation", ScaleInputMode.Hardware));
        Assert.True(ScaleInputModeDisplay.ShouldPersistMode("Simulation", ScaleInputMode.SimulationAutomatic));
        Assert.True(ScaleInputModeDisplay.ShouldPersistMode("Simulation", ScaleInputMode.SimulationManual));
    }

    [Fact]
    public void ScaleInputMode_OnlyHardwarePersists_OnHardwareDeployment()
    {
        Assert.True(ScaleInputModeDisplay.ShouldPersistMode("Hardware", ScaleInputMode.Hardware));
        Assert.False(ScaleInputModeDisplay.ShouldPersistMode("Hardware", ScaleInputMode.SimulationAutomatic));
        Assert.False(ScaleInputModeDisplay.ShouldPersistMode("Hardware", ScaleInputMode.SimulationManual));
    }

    [Fact]
    public void LatestFrameDisplay_ShowsPayloadWithoutStxEtx()
    {
        var display = ScaleFrameDisplay.FormatLatestFrame(new ScaleReading
        {
            RawFrame = ScaleFrameFixtures.Frame0Kg
        });
        Assert.Equal("+00000001B", display);
    }
}
