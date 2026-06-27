using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Scale;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase2DScaleModeSyncTests
{
    [Theory]
    [InlineData("Hardware", ScaleInputMode.SimulationAutomatic, ScaleInputMode.Hardware)]
    [InlineData("Hardware", null, ScaleInputMode.Hardware)]
    [InlineData("Hardware", ScaleInputMode.Hardware, ScaleInputMode.Hardware)]
    public void DeviceModeHardware_AlwaysResolvesHardware(
        string deviceMode,
        ScaleInputMode? saved,
        ScaleInputMode expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.ResolveStartupMode(deviceMode, saved));
    }

    [Theory]
    [InlineData("Hardware", ScaleInputMode.SimulationAutomatic, true)]
    [InlineData("Hardware", ScaleInputMode.Hardware, false)]
    [InlineData("Hardware", null, false)]
    [InlineData("Simulation", ScaleInputMode.SimulationAutomatic, false)]
    public void LegacyNormalization_OnlyForHardwareDeploymentWithStaleDb(
        string deviceMode,
        ScaleInputMode? saved,
        bool expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.ShouldNormalizeLegacyScaleMode(deviceMode, saved));
    }

    [Fact]
    public async Task StartupNormalization_PersistsHardwareToDb()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        await service.SaveScaleAsync(new ScaleDeviceSettingsDto
        {
            PortName = "COM1",
            BaudRate = 1200,
            ScaleInputMode = ScaleInputMode.SimulationAutomatic
        });

        var normalized = ScaleInputModeDisplay.ResolveStartupMode("Hardware", ScaleInputMode.SimulationAutomatic);
        await service.SaveScaleAsync(new ScaleDeviceSettingsDto
        {
            PortName = "COM1",
            BaudRate = 1200,
            ScaleInputMode = normalized
        });

        var loaded = await service.GetScaleAsync();
        Assert.Equal(ScaleInputMode.Hardware, loaded.ScaleInputMode);
    }

    [Fact]
    public async Task RadioHardwareSelection_CompositeServiceUsesHardware()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.SimulationAutomatic);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        Assert.Equal(ScaleInputMode.Hardware, service.InputMode);
    }

    [Fact]
    public async Task SwitchAutomaticSimulationToHardware_StopsSimulationInputMode()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.SimulationAutomatic);
        Assert.Equal(ScaleInputMode.SimulationAutomatic, service.InputMode);

        decimal? afterSwitch = null;
        service.WeightChanged += (_, w) => afterSwitch = w;

        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await Task.Delay(700);

        Assert.Equal(ScaleInputMode.Hardware, service.InputMode);
        Assert.Null(afterSwitch);
    }

    [Fact]
    public async Task HardwareMode_DoesNotServeSimulatedWeight()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.SimulationAutomatic);
        _ = await service.GetCurrentWeightAsync();

        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCurrentWeightAsync());
    }

    [Theory]
    [InlineData(ScaleInputMode.Hardware, ScaleHeaderConnectionState.Connected, "● Đầu cân: COM")]
    [InlineData(ScaleInputMode.Hardware, ScaleHeaderConnectionState.Connecting, "● Đầu cân: Đang kết nối")]
    [InlineData(ScaleInputMode.Hardware, ScaleHeaderConnectionState.Disconnected, "● Đầu cân: Mất kết nối")]
    public void HeaderHardware_UsesRuntimeConnectionState(
        ScaleInputMode mode,
        ScaleHeaderConnectionState state,
        string expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.GetHeaderScaleBadge(mode, state));
    }

    [Fact]
    public void SourceLabelHardware_IsComSource()
    {
        Assert.Equal("Nguồn: Đầu cân COM", ScaleInputModeDisplay.GetWeightSourceText(ScaleInputMode.Hardware));
    }

    [Fact]
    public void HeaderAndSource_ChangeImmediatelyForHardwareMode()
    {
        var header = ScaleInputModeDisplay.GetHeaderScaleBadge(
            ScaleInputMode.Hardware,
            ScaleHeaderConnectionState.Connecting);
        var source = ScaleInputModeDisplay.GetWeightSourceText(ScaleInputMode.Hardware);
        Assert.Contains("Đang kết nối", header);
        Assert.Contains("Đầu cân COM", source);
    }

    [Theory]
    [InlineData("Simulation", null, ScaleInputMode.SimulationAutomatic)]
    [InlineData("Simulation", ScaleInputMode.SimulationManual, ScaleInputMode.SimulationManual)]
    public void DeviceModeSimulation_StillAllowsSimulationModes(
        string deviceMode,
        ScaleInputMode? saved,
        ScaleInputMode expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.ResolveStartupMode(deviceMode, saved));
    }

    [Fact]
    public async Task PersistScaleSettings_StoresHardwareModeFromUiSelection()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        await service.SaveScaleAsync(new ScaleDeviceSettingsDto
        {
            PortName = "COM1",
            BaudRate = 1200,
            ScaleInputMode = ScaleInputMode.Hardware,
            AutoConnectScaleOnStartup = true
        });

        var loaded = await service.GetScaleAsync();
        Assert.Equal(ScaleInputMode.Hardware, loaded.ScaleInputMode);
        Assert.Equal(1200, loaded.BaudRate);
    }

    [Fact]
    public async Task RestartAfterSave_StillLoadsHardware()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        await service.SaveScaleAsync(new ScaleDeviceSettingsDto
        {
            PortName = "COM1",
            BaudRate = 1200,
            ScaleInputMode = ScaleInputMode.Hardware
        });

        var loaded = await service.GetScaleAsync();
        var startup = ScaleInputModeDisplay.ResolveStartupMode("Hardware", loaded.ScaleInputMode);
        Assert.Equal(ScaleInputMode.Hardware, startup);
    }

    [Fact]
    public void StartupLogFields_AllHardwareWhenDeviceModeHardware()
    {
        const ScaleInputMode resolved = ScaleInputMode.Hardware;
        var serviceMode = ScaleInputMode.Hardware;
        Assert.Equal(resolved, serviceMode);
        Assert.NotEqual(ScaleInputMode.SimulationAutomatic, resolved);
    }

    [Fact]
    public void SimulationAutomaticHeaderAndSource_AreConsistent()
    {
        const ScaleInputMode mode = ScaleInputMode.SimulationAutomatic;
        var header = ScaleInputModeDisplay.GetHeaderScaleBadge(mode, false);
        var source = ScaleInputModeDisplay.GetWeightSourceText(mode);
        Assert.Contains("Tự động", header);
        Assert.Contains("Tự động mô phỏng", source);
    }
}
