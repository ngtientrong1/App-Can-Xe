using CanXe.Application.Interfaces;

namespace CanXe.Tests.Application;

public class Phase4Rc19PrintWorkflowTests
{
    [Fact]
    public void IWeighTicketPrintWorkflow_HasRequiredMethods()
    {
        var methods = typeof(IWeighTicketPrintWorkflow).GetMethods();
        Assert.Contains(methods, m => m.Name == nameof(IWeighTicketPrintWorkflow.BuildPrintModelAsync));
        Assert.Contains(methods, m => m.Name == nameof(IWeighTicketPrintWorkflow.PrintTicketAsync));
        Assert.Contains(methods, m => m.Name == nameof(IWeighTicketPrintWorkflow.PreviewTicketAsync));
    }
}
