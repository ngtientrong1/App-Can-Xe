using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;
public class Phase2DOperatorUiAutoConnectTests
{
    [Fact]
    public void DefaultNavigationSection_IsWeighTicket()
    {
        Assert.Equal("WeighTicket", NavigationTabPolicy.GetDefaultSectionKey());
    }

    [Fact]
    public void CameraClosedLayout_Uses38And62Stars()
    {
        var (weigh, info) = WorkAreaLayoutCalculator.GetMainColumnStars(isCameraDrawerOpen: false);
        Assert.Equal(38, weigh);
        Assert.Equal(62, info);
    }

    [Fact]
    public void CameraOpenLayout_Uses35And45Stars()
    {
        var (weigh, info) = WorkAreaLayoutCalculator.GetMainColumnStars(isCameraDrawerOpen: true);
        Assert.Equal(35, weigh);
        Assert.Equal(45, info);
    }

    [Theory]
    [InlineData(1920, 340)]
    [InlineData(1600, 340)]
    [InlineData(1366, 280)]
    [InlineData(1100, 260)]
    public void CameraDrawerWidth_IsResponsive(double windowWidth, int expectedWidth)
    {
        Assert.Equal(expectedWidth, WorkAreaLayoutCalculator.GetCameraDrawerWidth(true, windowWidth));
    }

    [Theory]
    [InlineData(1366, 72)]
    [InlineData(1600, 84)]
    [InlineData(1920, 96)]
    public void LiveWeightFontSize_ScalesWithWindow(double windowWidth, double expectedFontSize)
    {
        Assert.Equal(expectedFontSize, WorkAreaLayoutCalculator.GetLiveWeightFontSize(windowWidth));
    }

    [Fact]
    public void HardwareDefaultAutoConnect_IsTrue()
    {
        Assert.True(ScaleAutoConnectPolicy.GetDefaultAutoConnect("Hardware"));
        Assert.False(ScaleAutoConnectPolicy.GetDefaultAutoConnect("Simulation"));
    }

    [Theory]
    [InlineData("Hardware", "Hardware", true, true)]
    [InlineData("Hardware", "Hardware", false, false)]
    [InlineData("Simulation", "SimulationAutomatic", true, false)]
    [InlineData("Hardware", "SimulationAutomatic", true, false)]
    public void ShouldAutoConnectOnStartup_RespectsModeAndSetting(
        string deviceMode,
        string scaleInputMode,
        bool autoConnectSetting,
        bool expected)
    {
        Assert.Equal(
            expected,
            ScaleAutoConnectPolicy.ShouldAutoConnectOnStartup(deviceMode, scaleInputMode, autoConnectSetting));
    }

    [Fact]
    public void AutoConnectRetry_StopsAfterThreeAttempts()
    {
        Assert.True(ScaleAutoConnectPolicy.ShouldRetry(0));
        Assert.True(ScaleAutoConnectPolicy.ShouldRetry(2));
        Assert.False(ScaleAutoConnectPolicy.ShouldRetry(3));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2000)]
    [InlineData(2, 5000)]
    public void AutoConnectRetry_Delays(int attemptIndex, int expectedMs)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), ScaleAutoConnectPolicy.GetRetryDelay(attemptIndex));
    }

    [Theory]
    [InlineData("10000", 10000)]
    [InlineData("10,000", 10000)]
    [InlineData("0", null)]
    [InlineData("", null)]
    [InlineData("-5", null)]
    public void UnitPrice_Parse(string input, int? expected)
    {
        var parsed = UnitPriceInputHelper.Parse(input);
        if (expected is null)
            Assert.Null(parsed);
        else
            Assert.Equal(expected.Value, parsed);
    }

    [Fact]
    public void UnitPrice_CommitFormatsThousands()
    {
        Assert.Equal("10,000", UnitPriceInputHelper.CommitDisplay("10000"));
    }

    [Fact]
    public void UnitPrice_EmptyCommitReturnsNull()
    {
        Assert.Null(UnitPriceInputHelper.CommitDisplay(string.Empty));
    }

    [Fact]
    public void UnitPrice_EditableForeground_IsDarkOnWhite()
    {
        Assert.True(UnitPriceInputHelper.IsEditableForeground(UnitPriceInputHelper.EditableForeground));
        Assert.Equal("White", UnitPriceInputHelper.EditableBackground);
    }

    [Fact]
    public void ScaleDeviceDefaults_UseCom1At1200()
    {
        var defaults = new ScaleDeviceSettingsDto();
        Assert.Equal("COM1", defaults.PortName);
        Assert.Equal(1200, defaults.BaudRate);
        Assert.Equal(8, defaults.DataBits);
        Assert.Equal("None", defaults.Parity);
        Assert.Equal("One", defaults.StopBits);
        Assert.Equal("None", defaults.Handshake);
        Assert.True(defaults.AutoConnectScaleOnStartup);
    }

    [Fact]
    public async Task ScaleSettings_PersistAutoConnectFlag()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        await service.SaveScaleAsync(new ScaleDeviceSettingsDto
        {
            PortName = "COM1",
            BaudRate = 1200,
            AutoConnectScaleOnStartup = false
        });

        var loaded = await service.GetScaleAsync();
        Assert.False(loaded.AutoConnectScaleOnStartup);
        Assert.Equal(1200, loaded.BaudRate);
    }

    [Fact]
    public void NarrowWindow_UsesCameraOverlayBreakpoint()
    {
        Assert.True(WorkAreaLayoutCalculator.ShouldUseCameraOverlay(1199));
        Assert.False(WorkAreaLayoutCalculator.ShouldUseCameraOverlay(1200));
    }
}
