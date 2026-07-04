using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc8PrintLayoutTests
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
  }

  [Fact]
  public void SectionHeights_FillRc18WeightFirstLayout()
  {
#pragma warning disable CS0618
    Assert.Equal(52, WeighTicketPrintLayout.DetailsSectionHeightMm);
#pragma warning restore CS0618
    Assert.Equal(137.5, WeighTicketPrintLayout.LayoutSectionsTotalHeightMm, 1);
  }

  [Fact]
  public void DetailsRatio_Is66By34()
  {
    Assert.Equal(66, WeighTicketPrintLayout.BodyLeftColumnStar);
    Assert.Equal(34, WeighTicketPrintLayout.BodyRightColumnStar);
  }

  [Fact]
  public void FieldColumns_Are34By66()
  {
    Assert.Equal(34, WeighTicketPrintLayout.FieldLabelColumnStar);
    Assert.Equal(66, WeighTicketPrintLayout.FieldValueColumnStar);
  }

  [Fact]
  public void HeroTypography_UsesPtToDip()
  {
    Assert.Equal(WeighTicketPrintLayout.PtToDip(30), WeighTicketPrintTypography.HeroWeightValueDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PtToDip(11.5), WeighTicketPrintTypography.HeroCardLabelDip, 4);
    Assert.Equal(WeighTicketPrintLayout.PtToDip(24), WeighTicketPrintTypography.TitleDip, 4);
  }

  [Fact]
  public void SignatureBlank_Meets20MillimeterMinimum()
  {
    Assert.Equal(20, WeighTicketPrintLayout.SignatureBlankHeightMm);
  }

  [Theory]
  [InlineData(180, "180")]
  [InlineData(170, "170")]
  [InlineData(10, "10")]
  [InlineData(11_730, "11.730")]
  public void FormatHeroWeightValue_UsesIntegerKilograms(decimal input, string expected)
  {
    var formatted = WeighTicketPrintFormatter.FormatHeroWeightValue(input, true);
    Assert.Equal(expected, formatted);
    Assert.DoesNotContain(",000", formatted, StringComparison.Ordinal);
  }

  [Fact]
  public void FormatHeroWeightValue_MissingEvent_ShowsDash()
  {
    Assert.Equal("—", WeighTicketPrintFormatter.FormatHeroWeightValue(180m, (DateTimeOffset?)null));
    Assert.Equal("—", WeighTicketPrintFormatter.FormatHeroWeightValue(0m, false));
  }

  [Fact]
  public void FormatHeroWeightValue_RecordedZero_ShowsZero()
  {
    Assert.Equal("0", WeighTicketPrintFormatter.FormatHeroWeightValue(0m, true));
  }

  [Fact]
  public void IndependentTimestamps_StillCrossMidnight()
  {
    var model = Phase4Rc6PixelAccurateTicketTests.BuildRc6SampleModel();
    Assert.Equal("29/06/2026", WeighTicketPrintFormatter.FormatDate(model.FirstWeighingAt));
    Assert.Equal("30/06/2026", WeighTicketPrintFormatter.FormatDate(model.SecondWeighingAt));
  }
}
