using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc12PrintSafeLayoutTests
{
    [Fact]
    public void SafeMargins_MatchRc18InternalSafeBox()
    {
        Assert.Equal(10, WeighTicketPrintLayout.SafeContentLeftMm);
        Assert.Equal(10, WeighTicketPrintLayout.SafeContentRightMm);
        Assert.Equal(190, WeighTicketPrintLayout.ContentWidthMm);
        Assert.Equal(200, WeighTicketPrintLayout.CopySafeRightEdgeMm);
    }

    [Fact]
    public void ContentWidth_FitsInsideSafeArea()
    {
        Assert.Equal(
            WeighTicketPrintLayout.TicketLogicalWidthMm,
            WeighTicketPrintLayout.SafeContentLeftMm + WeighTicketPrintLayout.ContentWidthMm + WeighTicketPrintLayout.SafeContentRightMm);
    }

    [Fact]
    public void CardBorder_IsUniform()
    {
        Assert.Equal(0.75, WeighTicketPrintLayout.CardBorderThicknessDip);
        Assert.Equal(WeighTicketPrintLayout.MmToDip(1.8), WeighTicketPrintLayout.CardCornerRadiusDip, 1);
    }

    [Fact]
    public void DetailsTable_HasRightPadding()
    {
        Assert.Equal(4.0, WeighTicketPrintLayout.DetailsTablePaddingRightMm);
        Assert.Equal(1.5, WeighTicketPrintLayout.DetailValueInsetMm);
    }

    [Fact]
    public void ImageableScale_ComputesOnce()
    {
        var scale = WeighTicketPrintLayout.ComputeImageableScale(750, 1100, 793.7, 1122.5);
        Assert.True(scale > 0.9 && scale < 1.0);
    }
}
