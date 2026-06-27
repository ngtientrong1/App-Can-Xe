using CanXe.Application.Models;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Scale;

namespace CanXe.Tests.Application;

public class Phase171ScaleInputModeTests
{
    [Fact]
    public void Simulation_DefaultMode_IsAutomatic()
    {
        var mode = ScaleInputModeDisplay.GetDefaultMode("Simulation");
        Assert.Equal(ScaleInputMode.SimulationAutomatic, mode);
    }

    [Fact]
    public void HardwareDeviceMode_DefaultMode_IsHardware()
    {
        var mode = ScaleInputModeDisplay.GetDefaultMode("Hardware");
        Assert.Equal(ScaleInputMode.Hardware, mode);
    }

    [Fact]
    public void ManualMode_PersistsWhenSelected()
    {
        Assert.True(ScaleInputModeDisplay.ShouldPersistMode(ScaleInputMode.SimulationManual));
        Assert.True(ScaleInputModeDisplay.ShouldPersistMode(ScaleInputMode.SimulationAutomatic));
        Assert.True(ScaleInputModeDisplay.ShouldPersistMode(ScaleInputMode.Hardware));
    }

    [Fact]
    public void ManualMode_OnlyEnabled_WhenExplicitlySelected()
    {
        Assert.False(ScaleInputModeDisplay.IsManualInputEnabled(ScaleInputMode.SimulationAutomatic));
        Assert.True(ScaleInputModeDisplay.IsManualInputEnabled(ScaleInputMode.SimulationManual));
    }

    [Fact]
    public void ReturnToAutomaticButton_VisibleOnlyInManualMode()
    {
        Assert.False(ScaleInputModeDisplay.IsReturnToAutomaticVisible(ScaleInputMode.SimulationAutomatic));
        Assert.True(ScaleInputModeDisplay.IsReturnToAutomaticVisible(ScaleInputMode.SimulationManual));
    }

    [Fact]
    public async Task ReturnToAutomatic_ResumesSimulatedScaleService()
    {
        await using var service = new SimulatedScaleService();
        await service.StartAsync();
        service.SetManualMode(true);
        service.SetManualWeightKg(8500m);

        Assert.True(service.IsManualMode);

        service.ResumeAutomaticSimulation();
        service.SetManualMode(false);

        Assert.False(service.IsManualMode);

        decimal? changed = null;
        service.WeightChanged += (_, w) => changed = w;
        await Task.Delay(700);
        Assert.NotNull(changed);
    }

    [Fact]
    public void DevDrawerClose_DoesNotChangeScaleMode()
    {
        Assert.False(ScaleInputModeDisplay.DevDrawerCloseChangesScaleMode());
    }

    [Theory]
    [InlineData(ScaleInputMode.SimulationAutomatic, false, "● Đầu cân: Tự động")]
    [InlineData(ScaleInputMode.SimulationManual, false, "● Đầu cân: DEV thủ công")]
    [InlineData(ScaleInputMode.SimulationAutomatic, true, "● Đầu cân: Mất kết nối")]
    public void HeaderBadge_ReflectsMode(ScaleInputMode mode, bool disconnected, string expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.GetHeaderScaleBadge(mode, disconnected));
    }

    [Theory]
    [InlineData("Simulation", false)]
    [InlineData("Hardware", true)]
    public void HardwareOption_EnabledOnlyInHardwareDeviceMode(string deviceMode, bool expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.IsHardwareOptionEnabled(deviceMode));
    }

    [Fact]
    public void DeveloperWeight1Override_IsIndependentOfScaleInputMode()
    {
        Assert.True(ScaleInputModeDisplay.IsDeveloperWeight1OverrideIndependentOfScaleMode());
        Assert.True(DraftWorkflowRules.CanUpdateWeight1(
            isWeight1LockedFromSaved: false,
            hasDraftWeight2: true,
            developerWeight1OverrideEnabled: true));
    }

    [Fact]
    public void EditTicketWeightUnlock_IsIndependentOfScaleInputMode()
    {
        Assert.True(ScaleInputModeDisplay.IsEditTicketWeightUnlockIndependentOfScaleMode());
    }

    [Theory]
    [InlineData("WeighTicket", "WeighTicket", true)]
    [InlineData("WeighTicket", "Settings", false)]
    [InlineData("Settings", "Settings", true)]
    public void NavigationTab_ActiveState(string active, string tab, bool expected)
    {
        Assert.Equal(expected, NavigationTabPolicy.IsTabActive(active, tab));
    }

    [Fact]
    public void DefaultNavigationSection_IsWeighTicket()
    {
        Assert.Equal("WeighTicket", NavigationTabPolicy.GetDefaultSectionKey());
    }

    [Fact]
    public void CameraDrawerClosed_LeavesNoGap()
    {
        var width = WorkAreaLayoutCalculator.GetCameraDrawerWidth(false);
        Assert.True(WorkAreaVisualStatePolicy.CameraDrawerLeavesNoGapWhenClosed(width));
    }

    [Fact]
    public void UtilityRail_AlwaysVisibleWhenConfigured()
    {
        var width = WorkAreaLayoutCalculator.GetUtilityRailWidth(isCompact: false);
        Assert.True(WorkAreaVisualStatePolicy.UtilityRailAlwaysVisible(width));
        Assert.InRange(width, 52, 64);
    }

    [Fact]
    public void FullHdLayout_Uses31And51StarsWhenCameraOpen()
    {
        var (weigh, info) = WorkAreaLayoutCalculator.GetMainColumnStars(isCameraDrawerOpen: true);
        Assert.Equal(31, weigh);
        Assert.Equal(51, info);
        Assert.Equal(300, WorkAreaLayoutCalculator.GetCameraDrawerWidth(true));
    }

    [Fact]
    public void FullHdLayout_ExpandsInfoWhenCameraClosed()
    {
        var (weigh, info) = WorkAreaLayoutCalculator.GetMainColumnStars(isCameraDrawerOpen: false);
        Assert.Equal(31, weigh);
        Assert.Equal(69, info);
        Assert.Equal(0, WorkAreaLayoutCalculator.GetCameraDrawerWidth(false));
    }

    [Fact]
    public void FooterSummary_HasFiveCards()
    {
        Assert.Equal(5, WorkAreaVisualStatePolicy.GetSummaryCardCount());
    }

    [Theory]
    [InlineData(1920, false)]
    [InlineData(1366, true)]
    [InlineData(1280, true)]
    public void CompactMode_Threshold(double width, bool expected)
    {
        Assert.Equal(expected, CompactLayoutPolicy.ShouldUseCompactMode(width));
    }

    [Fact]
    public void CompactMode_KeepsBothActionBarsDefined()
    {
        Assert.True(EditModeActionBarPolicy.IsCreateModeVisible(false));
        Assert.True(EditModeActionBarPolicy.IsEditModeVisible(true));
    }

    [Theory]
    [InlineData(ScaleInputMode.SimulationAutomatic, "Nguồn: Tự động mô phỏng")]
    [InlineData(ScaleInputMode.SimulationManual, "Nguồn: DEV thủ công")]
    public void WeightSourceText_MatchesMode(ScaleInputMode mode, string expected)
    {
        Assert.Equal(expected, ScaleInputModeDisplay.GetWeightSourceText(mode));
    }
}
