using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public class Phase4Rc13SeamlessBrowseTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void ListColumnText_CompletedIsBlank()
    {
        Assert.Equal("—", WeighTicketWorkflow.ListColumnText(WeighTicketWorkflowState.Completed));
    }

    [Fact]
    public void ListColumnText_AwaitingSecondWeighIsVisible()
    {
        Assert.Equal("CHỜ CÂN LẦN 2", WeighTicketWorkflow.ListColumnText(WeighTicketWorkflowState.AwaitingSecondWeigh));
    }

    [Fact]
    public async Task LoadTicketForView_DoesNotEnableEditFlags()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(18000m);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách A", DraftVehicle = "51A-1" };
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(17000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);

        var viewDraft = await service.LoadTicketForViewAsync(save.SavedTicket!.Id);
        Assert.False(viewDraft.IsEditMode);
        Assert.True(viewDraft.IsWeight1LockedFromSavedTicket);
        Assert.True(viewDraft.IsWeight2LockedFromSavedTicket);
        Assert.Equal(save.SavedTicket.Id, viewDraft.ExistingTicketId);
    }

    [Fact]
    public async Task LoadTicketForContinuation_LocksWeight1()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(18000m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);

        var continuation = await service.LoadTicketForContinuationAsync(save.SavedTicket!.Id);
        Assert.True(continuation.IsWeight1LockedFromSavedTicket);
        Assert.False(continuation.IsWeight2LockedFromSavedTicket);
        Assert.Equal(WeighTicketWorkflowState.AwaitingSecondWeigh, WeighTicketWorkflow.FromEventCount(1));
    }
}
