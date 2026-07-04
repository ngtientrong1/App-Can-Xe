using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc10PrintLayoutTests
{
    [Fact]
    public void SectionHeights_UnchangedFromRc9()
    {
        Assert.Equal(137.5, WeighTicketPrintLayout.LayoutSectionsTotalHeightMm, 1);
        Assert.Equal(52, WeighTicketPrintLayout.BodySectionHeightMm);
        Assert.Equal(19, WeighTicketPrintLayout.IdentityCardsHeightMm);
        Assert.Equal(30.5, WeighTicketPrintLayout.DetailsTableHeightMm);
    }

    [Fact]
    public void Rc14Spacing_UsesSpacerGeometry()
    {
        Assert.Equal(3.0, WeighTicketPrintLayout.WeightCardGapMm);
        Assert.Equal(3.0, WeighTicketPrintLayout.IdentityCardGapMm);
        Assert.Equal(2.5, WeighTicketPrintLayout.IdentityDetailsGapMm);
        Assert.Equal(3.0, WeighTicketPrintLayout.BodyLeftTimestampGapMm);
        Assert.Equal(2.0, WeighTicketPrintLayout.BodySignatureGapMm);
    }

    [Fact]
    public void Rc16CardBorder_Is08Dip()
    {
        Assert.Equal(0.75, WeighTicketPrintLayout.CardBorderThicknessDip);
    }

    [Fact]
    public void IdentityIconTile_Is12Point5Millimeters()
    {
        Assert.Equal(12.5, WeighTicketPrintLayout.IdentityIconTileWidthMm);
        Assert.Equal(7.75, WeighTicketPrintLayout.IdentityIconGraphicSizeMm);
    }

    [Fact]
    public void DetailsTablePadding_HasFourMillimeterRightInset()
    {
        Assert.Equal(3.0, WeighTicketPrintLayout.DetailsTablePaddingLeftMm);
        Assert.Equal(4.0, WeighTicketPrintLayout.DetailsTablePaddingRightMm);
        Assert.True(WeighTicketPrintLayout.DetailsTablePaddingRightDip >= WeighTicketPrintLayout.MmToDip(4) - 0.01);
    }

    [Fact]
    public void TimestampCardPadding_Is2Millimeters()
    {
        Assert.Equal(2.0, WeighTicketPrintLayout.TimestampCardPaddingMm);
    }

    [Fact]
    public void HeroUnitTypography_Is11Point5Pt()
    {
        Assert.Equal(WeighTicketPrintLayout.PtToDip(11.5), WeighTicketPrintTypography.HeroWeightUnitDip, 4);
    }
}
