using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc11PrintLayoutTests
{
    [Fact]
    public void DefaultPrintRenderingMode_IsRasterCompatibility()
    {
        Assert.Equal(PrintRenderingMode.RasterCompatibility, new PrintSettingsDto().PrintRenderingMode);
    }

    [Fact]
    public void CardBorder_Is08Dip()
    {
        Assert.Equal(0.75, WeighTicketPrintLayout.CardBorderThicknessDip);
    }

    [Fact]
    public void CardCornerRadius_Is2Mm()
    {
        Assert.Equal(1.8, WeighTicketPrintLayout.CardCornerRadiusMm, 1);
    }

    [Fact]
    public void RasterA4Dimensions_Are300Dpi()
    {
        var width = (int)Math.Round(WeighTicketPrintLayout.PageWidthMm / WeighTicketPrintLayout.MmPerInch * 300);
        var height = (int)Math.Round(WeighTicketPrintLayout.PageHeightMm / WeighTicketPrintLayout.MmPerInch * 300);
        Assert.Equal(2480, width);
        Assert.Equal(3508, height);
    }
}
