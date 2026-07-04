using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc14PrintLayoutGeometryTests
{
    [Fact]
    public void Rc14Constants_AreObsoleteAfterRc15()
    {
        Assert.Equal(0, WeighTicketPrintLayout.OuterFramePaddingMm);
        Assert.Equal(0, WeighTicketPrintLayout.OuterFrameBorderThicknessDip);
    }
}
