using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc6PixelAccurateTicketTests
{
  [Theory]
  [InlineData(23, 30.666666666666668)]
  [InlineData(18, 24)]
  [InlineData(14, 18.666666666666668)]
  [InlineData(12, 16)]
  [InlineData(10, 13.333333333333334)]
  [InlineData(27, 36)]
  [InlineData(22, 29.333333333333332)]
  public void PtToDip_ConvertsCorrectly(double pt, double expectedDip)
  {
    Assert.Equal(expectedDip, WeighTicketPrintLayout.PtToDip(pt), 4);
  }

  [Fact]
  public void TypographyDipValues_UsePtToDip()
  {
    Assert.Equal(WeighTicketPrintLayout.PtToDip(24), WeighTicketPrintTypography.TitleDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PtToDip(14), WeighTicketPrintTypography.StationNameDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PtToDip(10.5), WeighTicketPrintTypography.DetailLabelDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PtToDip(11.5), WeighTicketPrintTypography.DetailValueDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PtToDip(15.5), WeighTicketPrintTypography.WeighTimeDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PtToDip(10.5), WeighTicketPrintTypography.WeighDateDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PtToDip(12), WeighTicketPrintTypography.SignatureRoleDip, 4);
  }

  [Fact]
  public void EachCopy_IsExactly148Point5Millimeters()
  {
    Assert.Equal(WeighTicketPrintLayout.MmToDip(148.5), WeighTicketPrintLayout.CopyHeightDip, 4);
    Assert.Equal(WeighTicketPrintLayout.CopyHeightDip, WeighTicketPrintLayout.BottomCopyTopDip, 4);
    Assert.Equal(WeighTicketPrintLayout.CopyHeightDip * 2, WeighTicketPrintLayout.PageHeightDip, 4);
  }

  [Fact]
  public void ContentArea_MatchesRc18SafeMargins()
  {
    Assert.Equal(190, WeighTicketPrintLayout.ContentWidthMm);
    Assert.Equal(137.5, WeighTicketPrintLayout.ContentHeightMm);
  }

  [Fact]
  public void SectionHeights_MatchRc18WeightFirstReference()
  {
    Assert.Equal(137.5, WeighTicketPrintLayout.LayoutSectionsTotalHeightMm, 1);
    Assert.Equal(19, WeighTicketPrintLayout.HeaderSectionHeightMm);
    Assert.Equal(10.5, WeighTicketPrintLayout.TitleSectionHeightMm);
    Assert.Equal(26, WeighTicketPrintLayout.WeightHeroSectionHeightMm);
    Assert.Equal(2, WeighTicketPrintLayout.HeroBodyGapMm);
    Assert.Equal(52, WeighTicketPrintLayout.BodySectionHeightMm);
    Assert.Equal(2, WeighTicketPrintLayout.BodySignatureGapMm);
    Assert.Equal(26, WeighTicketPrintLayout.SignatureSectionHeightMm);
  }

  [Fact]
  public void BodyRatio_Is66By34()
  {
    Assert.Equal(66, WeighTicketPrintLayout.BodyLeftColumnStar);
    Assert.Equal(34, WeighTicketPrintLayout.BodyRightColumnStar);
  }

  [Fact]
  public void RightPadding_MeetsMinimum()
  {
    Assert.True(WeighTicketPrintLayout.BodyRightPaddingMm >= WeighTicketPrintLayout.BodyRightMinPaddingMm);
  }

  [Fact]
  public void SignatureBlank_MeetsMinimum()
  {
    Assert.True(WeighTicketPrintLayout.SignatureBlankHeightMm >= 20);
  }

  [Fact]
  public void IndependentTimestamps_CrossMidnight()
  {
    var model = BuildRc6SampleModel();
    Assert.Equal("29/06/2026", WeighTicketPrintFormatter.FormatDate(model.FirstWeighingAt));
    Assert.Equal("30/06/2026", WeighTicketPrintFormatter.FormatDate(model.SecondWeighingAt));
    Assert.Equal("23:58:10", WeighTicketPrintFormatter.FormatTime(model.FirstWeighingAt));
    Assert.Equal("00:12:05", WeighTicketPrintFormatter.FormatTime(model.SecondWeighingAt));
  }

  [Fact]
  public void Mapper_UsesRecordedAt_NotTicketDateTime()
  {
    var detail = new WeighTicketDetailDto
    {
      Id = 1,
      DisplayNumber = "68/06/NK",
      CustomerName = "Cân Dịch Vụ",
      LicensePlate = "82C08112",
      CargoTypeName = "Rơ tươi",
      TicketDateTime = new DateTimeOffset(2026, 6, 30, 15, 30, 0, TimeSpan.FromHours(7)),
      Weight1RecordedAt = new DateTimeOffset(2026, 6, 29, 23, 58, 10, TimeSpan.FromHours(7)),
      Weight2RecordedAt = new DateTimeOffset(2026, 6, 30, 0, 12, 5, TimeSpan.FromHours(7))
    };
    var model = WeighTicketPrintModelMapper.FromDetail(detail, new StationSettingsDto(), new PrintSettingsDto());
    Assert.Equal(detail.Weight1RecordedAt, model.FirstWeighingAt);
    Assert.Equal(detail.Weight2RecordedAt, model.SecondWeighingAt);
    Assert.NotEqual(detail.TicketDateTime, model.FirstWeighingAt);
  }

  [Fact]
  public void UnitPrice_RemainsVndPerKg()
  {
    var model = BuildRc6SampleModel();
    var formatted = WeighTicketPrintFormatter.FormatUnitPricePerKg(model.UnitPriceVndPerKg, model.ShowPrice);
    Assert.Contains("VNĐ/kg", formatted!, StringComparison.Ordinal);
  }

  [Fact]
  public void LegacyNullTimestamp_ShowsDash()
  {
    Assert.Equal("—", WeighTicketPrintFormatter.FormatDate(null));
    Assert.False(WeighTicketPrintFormatter.HasRecordedWeighTimestamp(null));
  }

  public static WeighTicketPrintModel BuildRc6SampleModel() =>
      WeighTicketPrintModelMapper.FromDetail(
          new WeighTicketDetailDto
          {
            Id = 1,
            DisplayNumber = "68/06/NK",
            CustomerName = "Cân Dịch Vụ",
            LicensePlate = "82C08112",
            CargoTypeName = "Rơ tươi",
            Notes = "Giao buổi chiều",
            Weight1Kg = 11_730m,
            Weight2Kg = 4_980m,
            GrossWeightKg = 11_730m,
            TareWeightKg = 4_980m,
            NetWeightKg = 6_750m,
            UnitPriceVndPerKg = 89_000m,
            TotalAmountVnd = 600_750_000m,
            Weight1RecordedAt = new DateTimeOffset(2026, 6, 29, 23, 58, 10, TimeSpan.FromHours(7)),
            Weight2RecordedAt = new DateTimeOffset(2026, 6, 30, 0, 12, 5, TimeSpan.FromHours(7)),
            TicketDateTime = new DateTimeOffset(2026, 6, 30, 15, 30, 0, TimeSpan.FromHours(7))
          },
          new StationSettingsDto
          {
            StationName = "TRẠM CÂN TIẾN TRỌNG",
            StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
            Address = "Thôn 7 Xã Ngọc Wang, Huyện Đăk Hà, Tỉnh Kon Tum",
            Phone = "0363261945",
            SignLocationName = "Kon Tum"
          },
          new PrintSettingsDto());
}
