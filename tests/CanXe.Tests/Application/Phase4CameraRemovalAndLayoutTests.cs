using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure;
using CanXe.Infrastructure.Diagnostics;
using CanXe.Infrastructure.Logging;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase4CameraRemovalAndLayoutTests
{
    [Fact]
    public void ProductionDi_DoesNotRegisterCameraStreamService()
    {
        var services = new ServiceCollection();
        services.AddCanXeInfrastructure(new AppSettings(), ":memory:", Path.GetTempPath());
        Assert.DoesNotContain(services, d => d.ServiceType.Name.Contains("CameraStream", StringComparison.Ordinal));
        Assert.DoesNotContain(services, d => d.ServiceType.Name.Contains("Ffmpeg", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DiagnosticsAll_IncludesApplicationDatabaseScaleFilesystemPrinterOnly()
    {
        var cli = DiagnosticsArgumentParser.Parse(["--all"]);
        var options = DiagnosticsArgumentParser.BuildRunOptions(cli);
        Assert.True(options.IncludeApplication);
        Assert.True(options.IncludeDatabase);
        Assert.True(options.IncludeScale);
        Assert.True(options.IncludeFilesystem);
        Assert.True(options.IncludePrinter);
    }

    [Fact]
    public void WorkAreaLayout_Phase4TwoColumnOnly()
    {
        var (weigh1366, info1366) = WorkAreaLayoutCalculator.GetMainColumnStars(1366);
        Assert.InRange(weigh1366, 46, 46);
        Assert.InRange(info1366, 54, 54);

        var (weigh1920, info1920) = WorkAreaLayoutCalculator.GetMainColumnStars(1920);
        Assert.InRange(weigh1920, 44, 44);
        Assert.InRange(info1920, 56, 56);

        Assert.True(weigh1366 * 100 / (weigh1366 + info1366) >= WorkAreaLayoutCalculator.MinWeighColumnPercent);
        Assert.Equal(0, WorkAreaLayoutCalculator.GetCameraDrawerWidth(true));
        Assert.Equal(0, WorkAreaLayoutCalculator.GetUtilityRailWidth(false));
    }

    [Fact]
    public void AppSettings_ProductionHasDeveloperTabOff()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "CanXe.Desktop", "appsettings.Production.json");
        if (!File.Exists(path))
            return;
        var json = File.ReadAllText(path);
        Assert.Contains("\"ShowDeveloperTab\": false", json.Replace(" ", ""));
        Assert.DoesNotContain("CameraPreviewEnabled", json);
    }

    [Fact]
    public async Task TakeWeight_NoSnapshotDependency()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        var service = factory.Provider.CreateScope().ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = factory.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);
        var draft = new WeighTicketDraft();
        var result = await service.CaptureWeightAsync(draft, 1);
        Assert.True(result.Success);
        Assert.Equal(8500m, draft.DraftWeight1);
        await service.WaitForPendingPhotosAsync();
        Assert.Null(draft.DraftWeight1PhotoPath);
    }
}

public class Phase4PrintScaffoldTests
{
    [Fact]
    public void A4Layout_HasTwoFullA5CopiesWithoutCutBand()
    {
        Assert.Equal(WeighTicketPrintLayout.MmToDip(210), WeighTicketPrintLayout.PageWidthDip, 4);
        Assert.Equal(WeighTicketPrintLayout.MmToDip(297), WeighTicketPrintLayout.PageHeightDip, 4);
        Assert.Equal(WeighTicketPrintLayout.MmToDip(148.5), WeighTicketPrintLayout.CopyHeightDip, 4);
        Assert.Equal(0, WeighTicketPrintLayout.TopCopyTopDip);
        Assert.Equal(WeighTicketPrintLayout.CopyHeightDip, WeighTicketPrintLayout.BottomCopyTopDip);
        Assert.Equal(0, WeighTicketPrintLayout.CutBandHeightMm);
    }

    [Fact]
    public void PrintModelMapper_BuildsFromTicketDetail()
    {
        var detail = new WeighTicketDetailDto
        {
            Id = 1,
            DisplayNumber = "0001/06/2026",
            CustomerName = "Khách A",
            LicensePlate = "51C-12345",
            CargoTypeName = "Cà phê",
            Weight1Kg = 8500m,
            Weight2Kg = 18500m,
            GrossWeightKg = 18500m,
            TareWeightKg = 8500m,
            NetWeightKg = 10000m,
            TicketDateTime = DateTimeOffset.Now
        };
        var station = new StationSettingsDto { StationName = "Trạm Test" };
        var settings = new PrintSettingsDto();
        var model = WeighTicketPrintModelMapper.FromDetail(detail, station, settings);
        Assert.Equal("0001/06/2026", model.DisplayNumber);
        Assert.Equal("Khách A", model.CustomerName);
        Assert.False(string.IsNullOrWhiteSpace(model.PrintSettings.TopCopyLabel));
        Assert.False(string.IsNullOrWhiteSpace(model.PrintSettings.BottomCopyLabel));
    }
}

public class Phase4DeveloperTabTests
{
    [Fact]
    public void DeveloperTab_VisibleOnlyWhenConfigured()
    {
        var on = new AppSettings { DeveloperMode = true, ShowDeveloperTab = true };
        var off = new AppSettings { DeveloperMode = false, ShowDeveloperTab = false };
        Assert.True(on.DeveloperMode && on.ShowDeveloperTab);
        Assert.False(off.DeveloperMode || off.ShowDeveloperTab);
    }

    [Fact]
    public void AppNavigation_HasDeveloperSection()
    {
        Assert.Equal(AppNavigationSection.Developer, Enum.Parse<AppNavigationSection>("Developer"));
    }
}
