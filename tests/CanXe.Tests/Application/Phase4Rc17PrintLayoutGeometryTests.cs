using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc17PrintLayoutGeometryTests
{
    [Fact]
    public void SectionHeights_IncludeHeroBodyAndBodySignatureSpacers()
    {
        Assert.Equal(26, WeighTicketPrintLayout.WeightHeroSectionHeightMm);
        Assert.Equal(2.0, WeighTicketPrintLayout.HeroBodyGapMm);
        Assert.Equal(52, WeighTicketPrintLayout.BodySectionHeightMm);
        Assert.Equal(2.0, WeighTicketPrintLayout.BodySignatureGapMm);
        Assert.Equal(26, WeighTicketPrintLayout.SignatureSectionHeightMm);
        Assert.Equal(137.5, WeighTicketPrintLayout.LayoutSectionsTotalHeightMm, 1);
    }

    [Fact]
    public void CardBorder_Is075DipAnd18MmRadiusWithCardHostInset()
    {
        Assert.Equal(0.75, WeighTicketPrintLayout.CardBorderThicknessDip);
        Assert.Equal(1.8, WeighTicketPrintLayout.CardCornerRadiusMm);
        Assert.Equal(0.4, WeighTicketPrintLayout.CardHostInsetMm);
    }

    [Fact]
    public void HorizontalGaps_Are3Millimeters()
    {
        Assert.Equal(3, WeighTicketPrintLayout.WeightCardGapMm);
        Assert.Equal(3, WeighTicketPrintLayout.IdentityCardGapMm);
        Assert.Equal(3, WeighTicketPrintLayout.BodyLeftTimestampGapMm);
    }

    [Fact]
    public void VerticalGaps_Are25Millimeters()
    {
        Assert.Equal(2.0, WeighTicketPrintLayout.HeroBodyGapMm);
        Assert.Equal(2.5, WeighTicketPrintLayout.IdentityDetailsGapMm);
        Assert.Equal(2.0, WeighTicketPrintLayout.BodySignatureGapMm);
    }

    [Fact]
    public void IdentityIconTileCorner_IsInsetFromOuterStroke()
    {
        Assert.True(WeighTicketPrintLayout.IdentityIconTileCornerRadiusDip <
                    WeighTicketPrintLayout.CardCornerRadiusDip);
    }
}
