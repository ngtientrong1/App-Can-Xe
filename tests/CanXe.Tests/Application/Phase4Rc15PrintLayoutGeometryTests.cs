using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc15PrintLayoutGeometryTests
{
    [Fact]
    public void Rc15Constants_SupersededByRc16()
    {
        Assert.Equal(0, WeighTicketPrintLayout.OuterFramePaddingMm);
        Assert.Equal(0.75, WeighTicketPrintLayout.CardBorderThicknessDip);
        Assert.Equal(1.8, WeighTicketPrintLayout.CardCornerRadiusMm);
    }
}
