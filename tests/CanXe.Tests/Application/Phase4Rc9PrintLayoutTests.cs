using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc9PrintLayoutTests
{
    [Fact]
    public void SectionHeights_FillRc9InnerContent()
    {
        Assert.Equal(19, WeighTicketPrintLayout.HeaderSectionHeightMm);
        Assert.Equal(10.5, WeighTicketPrintLayout.TitleSectionHeightMm);
        Assert.Equal(26, WeighTicketPrintLayout.WeightHeroSectionHeightMm);
        Assert.Equal(2, WeighTicketPrintLayout.HeroBodyGapMm);
        Assert.Equal(52, WeighTicketPrintLayout.BodySectionHeightMm);
        Assert.Equal(2, WeighTicketPrintLayout.BodySignatureGapMm);
        Assert.Equal(26, WeighTicketPrintLayout.SignatureSectionHeightMm);
        Assert.Equal(137.5, WeighTicketPrintLayout.LayoutSectionsTotalHeightMm, 1);
    }

    [Fact]
    public void BodySubsections_SumTo47Millimeters()
    {
        Assert.Equal(19, WeighTicketPrintLayout.IdentityCardsHeightMm);
        Assert.Equal(2.5, WeighTicketPrintLayout.IdentityDetailsGapMm);
        Assert.Equal(30.5, WeighTicketPrintLayout.DetailsTableHeightMm);
        Assert.Equal(21.8, WeighTicketPrintLayout.WeighBlock1HeightMm);
        Assert.Equal(45, WeighTicketPrintLayout.TimestampCardHeightMm);
        Assert.Equal(7, WeighTicketPrintLayout.SignDateRowHeightMm);
        Assert.Equal(52, WeighTicketPrintLayout.IdentityCardsHeightMm + WeighTicketPrintLayout.IdentityDetailsGapMm + WeighTicketPrintLayout.DetailsTableHeightMm);
        Assert.Equal(52, WeighTicketPrintLayout.TimestampCardHeightMm + WeighTicketPrintLayout.SignDateRowHeightMm);
    }

    [Fact]
    public void HeroTypography_UsesPtToDip()
    {
        Assert.Equal(WeighTicketPrintLayout.PtToDip(30), WeighTicketPrintTypography.HeroWeightValueDip, 4);
        Assert.Equal(WeighTicketPrintLayout.PtToDip(24), WeighTicketPrintTypography.TitleDip, 4);
        Assert.Equal(WeighTicketPrintLayout.PtToDip(16.5), WeighTicketPrintTypography.CustomerValueDip, 4);
        Assert.Equal(WeighTicketPrintLayout.PtToDip(18), WeighTicketPrintTypography.PlateValueDip, 4);
    }

    [Fact]
    public void SignatureBlank_Meets20MillimeterMinimum()
    {
        Assert.Equal(20, WeighTicketPrintLayout.SignatureBlankHeightMm);
    }

    [Theory]
    [InlineData(180, "180 kg")]
    [InlineData(11730, "11.730 kg")]
    [InlineData(4980, "4.980 kg")]
    public void FormatHeroWeightDisplay_UsesIntegerKilograms(decimal input, string expected)
    {
        Assert.Equal(expected, WeighTicketPrintFormatter.FormatHeroWeightDisplay(input, true));
        Assert.DoesNotContain(",000", WeighTicketPrintFormatter.FormatHeroWeightDisplay(input, true), StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHeroWeightDisplay_MissingEvent_ShowsDash()
    {
        Assert.Equal("—", WeighTicketPrintFormatter.FormatHeroWeightDisplay(180m, (DateTimeOffset?)null));
    }

    [Fact]
    public void IndependentTimestamps_StillCrossMidnight()
    {
        var model = Phase4Rc6PixelAccurateTicketTests.BuildRc6SampleModel();
        Assert.Equal("29/06/2026", WeighTicketPrintFormatter.FormatDate(model.FirstWeighingAt));
        Assert.Equal("30/06/2026", WeighTicketPrintFormatter.FormatDate(model.SecondWeighingAt));
    }
}
