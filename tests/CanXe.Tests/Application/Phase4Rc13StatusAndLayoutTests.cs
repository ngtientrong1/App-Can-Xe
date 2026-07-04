using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class Phase4Rc13StatusAndLayoutTests
{
    [Fact]
    public void CompletedStatus_IsBlankInListColumn()
    {
        Assert.Equal("—", WeighTicketWorkflow.ListColumnText(WeighTicketWorkflowState.Completed));
    }

    [Fact]
    public void AwaitingSecondWeighStatus_IsVisible()
    {
        Assert.Equal("CHỜ CÂN LẦN 2", WeighTicketWorkflow.ListColumnText(WeighTicketWorkflowState.AwaitingSecondWeigh));
    }

    [Fact]
    public void DraftStatus_ShowsChuaLuu()
    {
        Assert.Equal("CHƯA LƯU", WeighTicketWorkflow.ListColumnText(WeighTicketWorkflowState.Draft));
    }
}
