using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc16PrintLayoutGeometryTests
{
    [Fact]
    public void SectionHeights_MatchRc16Reference()
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
    public void BodySubsections_SumTo52Millimeters()
    {
        Assert.Equal(19, WeighTicketPrintLayout.IdentityCardsHeightMm);
        Assert.Equal(2.5, WeighTicketPrintLayout.IdentityDetailsGapMm);
        Assert.Equal(30.5, WeighTicketPrintLayout.DetailsTableHeightMm);
        Assert.Equal(45, WeighTicketPrintLayout.TimestampCardHeightMm);
        Assert.Equal(7, WeighTicketPrintLayout.SignDateRowHeightMm);
        Assert.Equal(52, WeighTicketPrintLayout.IdentityCardsHeightMm + WeighTicketPrintLayout.IdentityDetailsGapMm + WeighTicketPrintLayout.DetailsTableHeightMm);
        Assert.Equal(52, WeighTicketPrintLayout.TimestampCardHeightMm + WeighTicketPrintLayout.SignDateRowHeightMm);
    }

    [Fact]
    public void CardBorder_Was08DipInRc16()
    {
        Assert.Equal(0.75, WeighTicketPrintLayout.CardBorderThicknessDip);
        Assert.Equal(1.8, WeighTicketPrintLayout.CardCornerRadiusMm);
    }

    [Fact]
    public void HeaderRatio_Is57By43()
    {
        Assert.Equal(57, WeighTicketPrintLayout.HeaderLeftColumnStar);
        Assert.Equal(43, WeighTicketPrintLayout.HeaderRightColumnStar);
    }

    [Fact]
    public void IdentityAndTypography_MatchRc16()
    {
        Assert.Equal(12.5, WeighTicketPrintLayout.IdentityIconTileWidthMm);
        Assert.True(WeighTicketPrintLayout.IdentityIconGraphicSizeMm <= 8.0);
        Assert.Equal(16.5, WeighTicketPrintTypography.CustomerValuePt);
        // rc24: plate moved into the hero band alongside net weight, sized up to match.
        Assert.Equal(26.0, WeighTicketPrintTypography.PlateValuePt);
        Assert.Equal(9.0, WeighTicketPrintTypography.SignDatePt);
    }

    [Fact]
    public void SignatureBlank_Meets20MillimeterMinimum()
    {
        Assert.True(WeighTicketPrintLayout.SignatureBlankHeightMm >= 20);
    }
}
