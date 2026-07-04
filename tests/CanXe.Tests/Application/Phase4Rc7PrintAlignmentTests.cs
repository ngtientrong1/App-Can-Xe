using System.Globalization;
using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc7PrintAlignmentTests
{
  [Fact]
  public void SafeMargins_MatchRc18InternalSafeBox()
  {
    Assert.Equal(10, WeighTicketPrintLayout.SafeContentLeftMm);
    Assert.Equal(10, WeighTicketPrintLayout.SafeContentRightMm);
    Assert.Equal(5.5, WeighTicketPrintLayout.SafeContentTopMm);
    Assert.Equal(5.5, WeighTicketPrintLayout.SafeContentBottomMm);
    Assert.Equal(190, WeighTicketPrintLayout.ContentWidthMm);
    Assert.Equal(137.5, WeighTicketPrintLayout.ContentHeightMm);
    Assert.Equal(200, WeighTicketPrintLayout.CopySafeRightEdgeMm);
    Assert.Equal(143, WeighTicketPrintLayout.CopySafeBottomEdgeMm);
  }

  [Fact]
  public void SectionHeights_FillRc18InnerContent()
  {
    Assert.Equal(137.5, WeighTicketPrintLayout.LayoutSectionsTotalHeightMm, 1);
    Assert.Equal(52, WeighTicketPrintLayout.BodySectionHeightMm);
  }

  [Fact]
  public void HeaderRatio_Is58By42()
  {
    Assert.Equal(57, WeighTicketPrintLayout.HeaderLeftColumnStar);
    Assert.Equal(43, WeighTicketPrintLayout.HeaderRightColumnStar);
  }

  [Theory]
  [InlineData(180, "180 kg")]
  [InlineData(170, "170 kg")]
  [InlineData(10, "10 kg")]
  [InlineData(11_730, "11.730 kg")]
  [InlineData(4_980, "4.980 kg")]
  [InlineData(6_750, "6.750 kg")]
  [InlineData(0, "0 kg")]
  public void FormatWeightKg_UsesIntegerKilograms(decimal input, string expected)
  {
    Assert.Equal(expected, WeighTicketPrintFormatter.FormatWeightKg(input));
  }

  [Fact]
  public void FormatWeightKg_DoesNotUseThreeDecimalPlaces()
  {
    var formatted = WeighTicketPrintFormatter.FormatWeightKg(180m);
    Assert.DoesNotContain(",000", formatted, StringComparison.Ordinal);
    Assert.DoesNotContain("N3", formatted, StringComparison.Ordinal);
  }

  [Fact]
  public void FormatWeightKg_MissingValue_ShowsDash()
  {
    Assert.Equal("—", WeighTicketPrintFormatter.FormatWeightKg(null));
  }

  [Fact]
  public void IndependentTimestamps_StillCrossMidnight()
  {
    var model = Phase4Rc6PixelAccurateTicketTests.BuildRc6SampleModel();
    Assert.Equal("29/06/2026", WeighTicketPrintFormatter.FormatDate(model.FirstWeighingAt));
    Assert.Equal("30/06/2026", WeighTicketPrintFormatter.FormatDate(model.SecondWeighingAt));
  }

  [Fact]
  public void VietnameseCulture_FormatsThousandsWithDot()
  {
    var culture = CultureInfo.GetCultureInfo("vi-VN");
    Assert.Equal("11.730", (11730m).ToString("N0", culture));
  }
}
