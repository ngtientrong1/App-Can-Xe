using CanXe.Application.Configuration;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Scale;
using CanXe.Tests.Support;

namespace CanXe.Tests.Application;

public class Phase2DDeveloperModeTests
{
    [Fact]
    public void DeveloperModeDefault_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.DeveloperMode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void DeveloperMode_ControlsSimulationSelectionOnHardware(bool developerMode, bool expectedEnabled)
    {
        Assert.Equal(
            expectedEnabled,
            ScaleInputModeDisplay.IsSimulationSelectionEnabled("Hardware", developerMode));
    }

    [Fact]
    public void DevDrawerClose_DoesNotChangeScaleModePolicy()
    {
        Assert.False(ScaleInputModeDisplay.DevDrawerCloseChangesScaleMode());
    }

    [Theory]
    [InlineData("Hardware", ScaleInputMode.SimulationManual, false, false)]
    [InlineData("Hardware", ScaleInputMode.SimulationManual, true, true)]
    [InlineData("Hardware", ScaleInputMode.Hardware, false, true)]
    public void DeveloperMode_AllowsRuntimeSimulationOverrideOnHardware(
        string deviceMode,
        ScaleInputMode mode,
        bool developerMode,
        bool expectedValid)
    {
        Assert.Equal(expectedValid, ScaleInputModeDisplay.IsValidModeForDevice(deviceMode, mode, developerMode));
    }

    [Fact]
    public async Task ManualSimulationSelection_StopsHardwareConnection()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.ConnectHardwareAsync();

        await service.SetInputModeAsync(ScaleInputMode.SimulationManual);

        Assert.Equal(ScaleInputMode.SimulationManual, service.InputMode);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task ManualInput1730_UpdatesReading()
    {
        await using var service = new SimulatedScaleService();
        await service.StartAsync();
        service.SetManualMode(true);
        service.SetManualWeightKg(1730);

        var weight = await service.GetCurrentWeightAsync();
        Assert.Equal(1730m, weight);
    }

    [Fact]
    public async Task ManualInputZero_UpdatesReading()
    {
        await using var service = new SimulatedScaleService();
        await service.StartAsync();
        service.SetManualMode(true);
        service.SetManualWeightKg(0);

        var weight = await service.GetCurrentWeightAsync();
        Assert.Equal(0m, weight);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-100")]
    public void ManualInputNegative_IsRejected(string text)
    {
        Assert.False(ManualSimulationInputHelper.TryParseKg(text, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void ManualInputOverLimit_IsRejected()
    {
        Assert.False(ManualSimulationInputHelper.TryParseKg("1000000", out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void ManualInput1730_IsAccepted()
    {
        Assert.True(ManualSimulationInputHelper.TryParseKg("1730", out var kg, out var error));
        Assert.Equal(1730, kg);
        Assert.Null(error);
    }

    [Fact]
    public void ManualMode_SourceLabel_IsManualSimulation()
    {
        Assert.Equal(
            "Nguồn: Thủ công mô phỏng",
            ScaleInputModeDisplay.GetWeightSourceText(ScaleInputMode.SimulationManual));
    }

    [Fact]
    public void ManualMode_Header_IsManualBadge()
    {
        var header = ScaleInputModeDisplay.GetHeaderScaleBadge(
            ScaleInputMode.SimulationManual,
            ScaleHeaderConnectionState.Connected);
        Assert.Contains("Thủ công", header);
    }

    [Fact]
    public async Task SwitchManualToHardware_ClearsSimulatedReading()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.SimulationManual);
        service.SetManualWeightKg(8500);

        await service.SetInputModeAsync(ScaleInputMode.Hardware);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCurrentWeightAsync());
    }

    [Fact]
    public void HardwareAutoConnectPolicy_StillRequiresHardwareMode()
    {
        Assert.True(ScaleAutoConnectPolicy.ShouldAutoConnectOnStartup("Hardware", "Hardware", true));
        Assert.False(ScaleAutoConnectPolicy.ShouldAutoConnectOnStartup("Hardware", "SimulationManual", true));
    }

    [Fact]
    public void RestartHardwareProduction_ReturnsHardware_NotManualOverride()
    {
        var persisted = ScaleInputModeDisplay.GetPersistedScaleInputMode(
            "Hardware",
            ScaleInputMode.SimulationManual);
        Assert.Equal(ScaleInputMode.Hardware, persisted);

        var startup = ScaleInputModeDisplay.ResolveStartupMode("Hardware", persisted);
        Assert.Equal(ScaleInputMode.Hardware, startup);
    }

    [Fact]
    public void DeveloperModeFalse_DoesNotChangeHardwareStartupPrecedence()
    {
        Assert.Equal(
            ScaleInputMode.Hardware,
            ScaleInputModeDisplay.ResolveStartupMode("Hardware", ScaleInputMode.SimulationAutomatic));
    }

    [Fact]
    public void SaveOnHardwareDeployment_PersistsHardwareEvenWhenRuntimeWasManual()
    {
        var persisted = ScaleInputModeDisplay.GetPersistedScaleInputMode(
            "Hardware",
            ScaleInputMode.SimulationManual);
        Assert.Equal(ScaleInputMode.Hardware, persisted);
    }
}
