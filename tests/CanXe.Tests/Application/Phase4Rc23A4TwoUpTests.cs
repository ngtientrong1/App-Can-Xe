using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using Xunit;

namespace CanXe.Tests.Application;

public sealed class Phase4Rc23A4TwoUpTests
{
    [Fact]
    public void ProductionPolicy_AlwaysReturnsA4TwoUp()
    {
        var settings = new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A5SingleTicket };
        Assert.Equal(PrintLayoutMode.A4TwoUp, ProductionPrintLayoutPolicy.ResolveProductionLayoutMode(settings));
    }

    [Fact]
    public void A4Page_Is210x297Mm()
    {
        Assert.Equal(210, WeighTicketPrintLayout.PageWidthMm);
        Assert.Equal(297, WeighTicketPrintLayout.PageHeightMm);
    }

    [Fact]
    public void TwoUpCopies_EachHalfPage()
    {
        Assert.Equal(0, WeighTicketPrintLayout.TopCopyTopDip);
        Assert.Equal(WeighTicketPrintLayout.TicketLogicalHeightDip, WeighTicketPrintLayout.BottomCopyTopDip);
        Assert.Equal(
            WeighTicketPrintLayout.PageHeightDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip * 2);
    }

    [Fact]
    public void NormalizeStoredLayoutMode_ConvertsA5SingleTicket()
    {
        Assert.Equal(
            nameof(PrintLayoutMode.A4TwoUp),
            ProductionPrintLayoutPolicy.NormalizeStoredLayoutMode("A5SingleTicket"));
    }
}
