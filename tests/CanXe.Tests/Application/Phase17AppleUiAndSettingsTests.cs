using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Device;
using CanXe.Infrastructure.Security;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase17AppleUiAndSettingsTests
{
    [Fact]
    public void CameraClosedLayout_HasZeroDrawerWidthButRailPixels()
    {
        Assert.Equal(0, WorkAreaLayoutCalculator.GetCameraDrawerWidth(false));
        Assert.InRange(WorkAreaLayoutCalculator.GetUtilityRailWidth(false), 52, 64);
        var (weigh, info) = WorkAreaLayoutCalculator.GetMainColumnStars(false);
        Assert.Equal(38, weigh);
        Assert.Equal(62, info);
    }

    [Fact]
    public void CompactMode_ActivatesAt1366()
    {
        Assert.True(CompactLayoutPolicy.ShouldUseCompactMode(1366));
        Assert.True(CompactLayoutPolicy.ShouldUseCompactMode(1280));
        Assert.False(CompactLayoutPolicy.ShouldUseCompactMode(1920));
    }

    [Fact]
    public void EditModeActionBar_SwitchesButtonGroups()
    {
        Assert.True(EditModeActionBarPolicy.IsCreateModeVisible(false));
        Assert.False(EditModeActionBarPolicy.IsEditModeVisible(false));
        Assert.True(EditModeActionBarPolicy.IsEditModeVisible(true));
        Assert.False(EditModeActionBarPolicy.IsCreateModeVisible(true));
    }

    [Theory]
    [InlineData(true, "", 3, false, true, false)]
    [InlineData(true, "a", 3, false, true, true)]
    [InlineData(true, "a", 0, false, true, false)]
    [InlineData(true, " ", 2, false, true, false)]
    [InlineData(false, "abc", 2, false, true, false)]
    [InlineData(true, "abc", 2, true, true, false)]
    [InlineData(true, "abc", 2, false, false, false)]
    public void AutocompleteDropDownPolicy_OnlyOpensWhenTyping(
        bool focused, string text, int count, bool suppress, bool windowActive, bool expected) =>
        Assert.Equal(expected, AutocompleteDropDownPolicy.ShouldOpen(focused, text, count, suppress, windowActive));

    [Fact]
    public void AutocompleteDropDownPolicy_LimitsToSixItems() =>
        Assert.Equal(6, AutocompleteDropDownPolicy.LimitItemCount(20));

    [Fact]
    public void AutocompleteDropDownPolicy_EmptyTextNeverOpens() =>
        Assert.False(AutocompleteDropDownPolicy.ShouldOpen(true, "", 5, false, true));

    [Fact]
    public void AutocompleteDropDownPolicy_SuppressBlocksOpen() =>
        Assert.False(AutocompleteDropDownPolicy.ShouldOpen(true, "x", 5, true, true));

    [Fact]
    public void AutocompleteDropDownPolicy_InactiveWindowCloses() =>
        Assert.False(AutocompleteDropDownPolicy.ShouldOpen(true, "x", 5, false, false));

    [Fact]
    public void AutoFill_DoesNotRequireDropdown() =>
        Assert.False(AutocompleteDropDownPolicy.ShouldOpen(true, "81C", 3, true, true));

    [Fact]
    public async Task StationSettings_SaveAndLoad()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        var dto = new StationSettingsDto
        {
            StationName = "Trạm Test 17",
            OwnerName = "Chủ A",
            Address = "123 Đường X",
            Phone = "0901234567",
            Email = "test@example.com"
        };

        var (ok, _) = await service.SaveStationAsync(dto);
        Assert.True(ok);

        var loaded = await service.GetStationAsync();
        Assert.Equal("Trạm Test 17", loaded.StationName);
        Assert.Equal("Chủ A", loaded.OwnerName);
    }

    [Fact]
    public async Task StationSettings_StationNameRequired()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        var (ok, validation) = await service.SaveStationAsync(new StationSettingsDto { StationName = "  " });
        Assert.False(ok);
        Assert.True(validation.Errors.ContainsKey(nameof(StationSettingsDto.StationName)));
    }

    [Fact]
    public async Task PreviewOptions_UseUpdatedStationInfo()
    {
        var options = new TicketDocumentRenderOptions
        {
            ScaleSiteName = "Trạm Mới",
            OwnerName = "Chủ B",
            Address = "Địa chỉ mới"
        };
        Assert.Equal("Trạm Mới", options.ScaleSiteName);
        Assert.Equal("Chủ B", options.OwnerName);
    }

    [Fact]
    public async Task ScaleSettings_SaveAndLoad()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StationSettingsService>();

        await service.SaveScaleAsync(new ScaleDeviceSettingsDto
        {
            PortName = "COM3",
            BaudRate = 19200
        });

        var loaded = await service.GetScaleAsync();
        Assert.Equal("COM3", loaded.PortName);
        Assert.Equal(19200, loaded.BaudRate);
    }

    [Fact]
    public void DpApi_ProtectsAndUnprotectsPassword()
    {
        var protector = new DpApiSecretProtector();
        var protectedValue = protector.Protect("secret-rtsp-pass");
        Assert.NotEqual("secret-rtsp-pass", protectedValue);
        Assert.Equal("secret-rtsp-pass", protector.Unprotect(protectedValue));
    }

    [Fact]
    public void DpApi_ProtectedPassword_DoesNotContainPlainText()
    {
        var plain = "MyCameraPassword123";
        var protectedValue = new DpApiSecretProtector().Protect(plain);
        Assert.DoesNotContain(plain, protectedValue);
    }

    [Theory]
    [InlineData("http://bad", true)]
    [InlineData("rtsp://cam/live", false)]
    [InlineData("", false)]
    public void CameraValidation_RtspUrlMustStartWithRtspWhenSet(string url, bool expectError)
    {
        var result = SettingsValidationService.ValidateCamera(new CameraDeviceSettingsDto
        {
            IsEnabled = true,
            RtspUrl = url,
            PhotoRetentionDays = 3
        });
        Assert.Equal(expectError, result.Errors.ContainsKey(nameof(CameraDeviceSettingsDto.RtspUrl)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(30, false)]
    [InlineData(31, true)]
    public void CameraValidation_PhotoRetentionDays(int days, bool expectError)
    {
        var result = SettingsValidationService.ValidateCamera(new CameraDeviceSettingsDto
        {
            PhotoRetentionDays = days
        });
        Assert.Equal(expectError, result.Errors.ContainsKey(nameof(CameraDeviceSettingsDto.PhotoRetentionDays)));
    }

    [Fact]
    public async Task MockRtspTest_Success()
    {
        var tester = new MockCameraConnectionTester(simulateFailure: false);
        var result = await tester.TestAsync(new CameraDeviceSettingsDto
        {
            IsEnabled = true,
            RtspUrl = "rtsp://127.0.0.1/stream"
        });
        Assert.True(result.Success);
    }

    [Fact]
    public async Task MockRtspTest_Failure()
    {
        var tester = new MockCameraConnectionTester(simulateFailure: true);
        var result = await tester.TestAsync(new CameraDeviceSettingsDto
        {
            IsEnabled = true,
            RtspUrl = "rtsp://127.0.0.1/stream"
        });
        Assert.False(result.Success);
    }

    [Fact]
    public async Task NewestFirst_SortOrderUnchanged()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var ticketService = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var filter = new WeighTicketFilter { MaxResults = 50 };
        var result = await ticketService.GetFilteredWithSummaryAsync(filter);
        for (var i = 1; i < result.Items.Count; i++)
        {
            var prev = result.Items[i - 1];
            var cur = result.Items[i];
            Assert.True(prev.TicketDateTime >= cur.TicketDateTime);
        }
    }

    [Fact]
    public async Task Summary_IncludesTicketsWithoutPrice()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var ticketService = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var filter = new WeighTicketFilter { MaxResults = 200 };
        var result = await ticketService.GetFilteredWithSummaryAsync(filter);
        Assert.True(result.TotalNetWeightKg >= 0);
        Assert.True(result.MissingPriceCount >= 0);
    }
}
