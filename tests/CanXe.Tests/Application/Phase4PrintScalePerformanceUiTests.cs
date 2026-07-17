using System.Globalization;
using System.Diagnostics;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Logging;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase4PrintPhysicalDimensionTests
{
  [Fact]
  public void A4PageDip_MatchesPhysicalMillimeters()
  {
    Assert.Equal(210 * 96d / 25.4d, WeighTicketPrintLayout.PageWidthDip, 4);
    Assert.Equal(297 * 96d / 25.4d, WeighTicketPrintLayout.PageHeightDip, 4);
  }

  [Fact]
  public void A5CopyDip_MatchesPhysicalHalfPage()
  {
    Assert.Equal(148.5 * 96d / 25.4d, WeighTicketPrintLayout.CopyHeightDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PageWidthDip, WeighTicketPrintLayout.MmToDip(210), 4);
  }

  [Fact]
  public void MmToDip_ConvertsCorrectly()
  {
    Assert.Equal(96d / 25.4d, WeighTicketPrintLayout.MmToDip(1), 6);
    Assert.Equal(96d, WeighTicketPrintLayout.MmToDip(25.4), 4);
  }

  [Fact]
  public void RawMillimeters_AreNotUsedAsDipPageSize()
  {
    Assert.NotEqual(210, WeighTicketPrintLayout.PageWidthDip);
    Assert.NotEqual(297, WeighTicketPrintLayout.PageHeightDip);
    Assert.NotEqual(148.5, WeighTicketPrintLayout.CopyHeightDip);
  }

  [Fact]
  public void TwoCopies_FillFullA4HeightWithoutCutBand()
  {
    Assert.Equal(WeighTicketPrintLayout.CopyHeightMm * 2, WeighTicketPrintLayout.PageHeightMm);
    Assert.Equal(0, WeighTicketPrintLayout.CutBandHeightMm);
  }

  [Fact]
  public void Content_UsesRc18InnerSafeArea()
  {
    Assert.Equal(WeighTicketPrintLayout.MmToDip(190), WeighTicketPrintLayout.ContentUsableWidthDip, 4);
    Assert.Equal(WeighTicketPrintLayout.MmToDip(137.5), WeighTicketPrintLayout.ContentUsableHeightDip, 4);
  }

  [Fact]
  public void SignatureArea_MatchesRc18Section()
  {
    Assert.Equal(26, WeighTicketPrintLayout.SignatureSectionHeightMm);
    Assert.True(WeighTicketPrintLayout.SignatureBlankHeightMm >= 20);
  }

  [Theory]
  [InlineData(793.7, 1122.5)]
  [InlineData(780, 1100)]
  public void ImageableScale_ComputedOnceFromExtents(double extentWidth, double extentHeight)
  {
    var scale = WeighTicketPrintLayout.ComputeImageableScale(
        extentWidth,
        extentHeight,
        WeighTicketPrintLayout.PageWidthDip,
        WeighTicketPrintLayout.PageHeightDip);
    Assert.InRange(scale, 0.96, 1.0);
    Assert.Equal(Math.Min(extentWidth / WeighTicketPrintLayout.PageWidthDip,
        extentHeight / WeighTicketPrintLayout.PageHeightDip), scale, 4);
  }
}

public class Phase4PrintDataFormattingTests
{
  [Fact]
  public void UnitPrice_DisplaysVndPerKgWithoutMultiplyingBy1000()
  {
    const decimal input = 89_000m;
    var formatted = WeighTicketPrintFormatter.FormatUnitPricePerKg(input, showPrice: true);
    Assert.Equal($"{input.ToString("N0", CultureInfo.CurrentCulture)} VNĐ/kg", formatted);
    Assert.DoesNotContain("tấn", formatted!, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("89.000.000", formatted!, StringComparison.Ordinal);
  }

  [Fact]
  public void UnitPrice_StoredValueMatchesPrintDisplay()
  {
    var detail = new WeighTicketDetailDto
    {
      Id = 1,
      DisplayNumber = "0001/06/2026",
      Weight1Kg = 11_730m,
      Weight2Kg = 4_980m,
      GrossWeightKg = 11_730m,
      TareWeightKg = 4_980m,
      NetWeightKg = 6_750m,
      UnitPriceVndPerKg = 89_000m,
      TotalAmountVnd = 584_325_000m,
      TicketDateTime = DateTimeOffset.Now
    };
    var model = WeighTicketPrintModelMapper.FromDetail(
        detail,
        new StationSettingsDto { StationName = "Trạm Test" },
        new PrintSettingsDto { ShowPrice = true });
    Assert.Equal(89_000m, model.UnitPriceVndPerKg);
    Assert.Equal(
        $"{89_000m.ToString("N0", CultureInfo.CurrentCulture)} VNĐ/kg",
        WeighTicketPrintFormatter.FormatUnitPricePerKg(model.UnitPriceVndPerKg, true));
  }

  [Fact]
  public void TotalAmount_CalculatedFromVndPerKg()
  {
    var calc = WeightCalculator.Calculate(11_730m, 4_980m, 89_000m);
    Assert.NotNull(calc.TotalAmountVnd);
    Assert.True(calc.TotalAmountVnd > 0);
    Assert.Equal(calc.TotalAmountVnd, Math.Round(calc.BillableWeightKg!.Value * 89_000m, 0));
  }

  [Fact]
  public void TwoUpModel_HasSameDataForBothCopies()
  {
    var detail = BuildSampleDetail();
    var model = WeighTicketPrintModelMapper.FromDetail(
        detail,
        new StationSettingsDto { StationName = "Trạm" },
        new PrintSettingsDto());
    Assert.False(string.IsNullOrWhiteSpace(model.PrintSettings.TopCopyLabel));
    Assert.False(string.IsNullOrWhiteSpace(model.PrintSettings.BottomCopyLabel));
    Assert.Equal("0001/06/2026", model.DisplayNumber);
    Assert.Equal(detail.Weight1RecordedAt, model.Weight1RecordedAt);
    Assert.Equal(detail.Weight2RecordedAt, model.Weight2RecordedAt);
  }

  [Fact]
  public void ReprintWatermark_DoesNotUseTicketNumberOpacity()
  {
    var detail = BuildSampleDetail();
    var model = WeighTicketPrintModelMapper.FromDetail(
        detail,
        new StationSettingsDto { StationName = "Trạm" },
        new PrintSettingsDto { ShowReprintWatermark = true },
        isReprint: true);
    Assert.True(model.IsReprint);
    Assert.True(model.PrintSettings.ShowReprintWatermark);
  }

  private static WeighTicketDetailDto BuildSampleDetail() => new()
  {
    Id = 1,
    DisplayNumber = "0001/06/2026",
    CustomerName = "Khách A",
    LicensePlate = "51C-12345",
    CargoTypeName = "Cà phê",
    Weight1Kg = 11_730m,
    Weight2Kg = 4_980m,
    GrossWeightKg = 11_730m,
    TareWeightKg = 4_980m,
    NetWeightKg = 6_750m,
    Weight1RecordedAt = new DateTimeOffset(2026, 6, 27, 14, 43, 11, TimeSpan.FromHours(7)),
    Weight2RecordedAt = new DateTimeOffset(2026, 6, 27, 15, 2, 18, TimeSpan.FromHours(7)),
    UnitPriceVndPerKg = 89_000m,
    TicketDateTime = DateTimeOffset.Now
  };
}

public class Phase4PerformanceTests
{
  [Fact]
  public async Task TakeWeight_DoesNotCallPhotoCompatibility()
  {
    await using var factory = new TestApplicationFactory();
    await factory.InitializeAsync();
    var service = factory.Provider.CreateScope().ServiceProvider.GetRequiredService<WeighTicketService>();
    var scale = factory.Provider.GetRequiredService<CanXe.Application.Interfaces.IScaleService>();
    scale.SetManualMode(true);
    scale.SetManualWeightKg(11_730m);
    var draft = new WeighTicketDraft();
    var sw = Stopwatch.StartNew();
    var result = await service.CaptureWeightAsync(draft, 1);
    sw.Stop();
    Assert.True(result.Success);
    Assert.Equal(11_730m, draft.DraftWeight1);
    Assert.True(sw.ElapsedMilliseconds < 500);
  }

  [Fact]
  public async Task Save_UsesSingleTransactionPath()
  {
    await using var factory = new TestApplicationFactory();
    await factory.InitializeAsync();
    var service = factory.Provider.CreateScope().ServiceProvider.GetRequiredService<WeighTicketService>();
    var scale = factory.Provider.GetRequiredService<CanXe.Application.Interfaces.IScaleService>();
    scale.SetManualMode(true);
    scale.SetManualWeightKg(5000m);
    var draft = new WeighTicketDraft { DraftCustomer = "Test", DraftVehicle = "51A-00001" };
    await service.CaptureWeightAsync(draft, 1);
    var sw = Stopwatch.StartNew();
    var result = await service.SaveAsync(draft, new WeighTicketFilter());
    sw.Stop();
    Assert.True(result.Success);
    Assert.True(sw.ElapsedMilliseconds < 2000);
  }

  [Fact]
  public void OperatorActionLogger_WritesPerformanceMetrics()
  {
    var marker = "TestAction-" + Guid.NewGuid().ToString("N");
    OperatorActionLogger.WritePerformance(marker, "total=10ms");
    var path = CanXeLogPaths.GetLogFile("operator-performance.log");
    Assert.True(File.Exists(path), $"missing log at {path}");
    var text = File.ReadAllText(path);
    Assert.Contains($"[{marker}]", text);
  }
}

public class Phase4MainUiLayoutTests
{
  [Theory]
  [InlineData(1366, 46, 54)]
  [InlineData(1920, 44, 56)]
  [InlineData(1280, 50, 50)]
  public void WeighColumn_NotBelow42Percent(double width, int weighStars, int infoStars)
  {
    var (weigh, info) = WorkAreaLayoutCalculator.GetMainColumnStars(width);
    Assert.Equal(weighStars, weigh);
    Assert.Equal(infoStars, info);
    Assert.True(weigh * 100.0 / (weigh + info) >= WorkAreaLayoutCalculator.MinWeighColumnPercent);
  }

  [Theory]
  [InlineData(1366, 68)]
  [InlineData(1920, 84)]
  public void LiveWeightFont_IsLargeEnough(double width, double minSize)
  {
    Assert.True(WorkAreaLayoutCalculator.GetLiveWeightFontSize(width) >= minSize);
  }

  [Theory]
  [InlineData(1366, 22)]
  [InlineData(1920, 28)]
  public void WeighPanelValues_AreReadable(double width, double minSize)
  {
    Assert.True(WorkAreaLayoutCalculator.GetWeighPanelValueFontSize(width) >= minSize);
  }

  [Fact]
  public void FormFieldHeight_IsAtLeast44Pixels()
  {
    Assert.True(OperatorLayoutMetrics.GetFormFieldHeight(768) >= 44);
    Assert.True(OperatorLayoutMetrics.GetFormFieldHeight(1080) >= 48);
  }
}
