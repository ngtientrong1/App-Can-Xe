using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public class Phase4Rc12TwoStageWorkflowTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task WithServiceAsync(Func<WeighTicketService, Task> action)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<WeighTicketService>());
    }

    private async Task<T> WithServiceAsync<T>(Func<WeighTicketService, Task<T>> action)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<WeighTicketService>());
    }

    private IScaleService CreateScale() =>
        _factory.Provider.GetRequiredService<IScaleService>();

    [Fact]
    public async Task SaveAfterWeigh1_PersistsSequence1_AwaitingSecondWeigh()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);
            scale.SetManualWeightKg(18000m);

            var draft = new WeighTicketDraft { DraftCustomer = "Khách Hàng C", DraftVehicle = "51A-12345" };
            await service.CaptureWeightAsync(draft, 1);
            var save = await service.SaveAsync(draft);

            Assert.True(save.Success);
            Assert.Equal(WeighTicketWorkflowState.AwaitingSecondWeigh, save.WorkflowState);
            Assert.Equal(1, save.SavedTicket!.EventCount);
            Assert.Equal("CHỜ CÂN LẦN 2", save.SavedTicket.WorkflowStatusText);

            var detail = await service.GetTicketDetailAsync(save.SavedTicket.Id);
            Assert.Equal(18000m, detail.Weight1Kg);
            Assert.Null(detail.Weight2Kg);
        });
    }

    [Fact]
    public async Task Continuation_AllowsWeigh2WithoutUnlock()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft();
            scale.SetManualWeightKg(18000m);
            await service.CaptureWeightAsync(draft, 1);
            var first = await service.SaveAsync(draft);

            var loaded = await service.LoadTicketForContinuationAsync(first.SavedTicket!.Id);
            Assert.True(loaded.IsWeight1LockedFromSavedTicket);
            Assert.False(loaded.IsWeight2LockedFromSavedTicket);
            Assert.True(DraftWorkflowRules.CanUpdateWeight2(loaded.IsWeight2LockedFromSavedTicket));
            Assert.Equal(first.SavedTicket.DisplayNumber, loaded.DisplayNumber);
            Assert.Equal(first.SavedTicket.Id, loaded.ExistingTicketId);

            scale.SetManualWeightKg(17000m);
            var capture = await service.CaptureWeightAsync(loaded, 2);
            Assert.True(capture.Success);
        });
    }

    [Fact]
    public async Task SaveAfterWeigh2_CompletesSameTicket()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft();
            scale.SetManualWeightKg(18000m);
            await service.CaptureWeightAsync(draft, 1);
            var first = await service.SaveAsync(draft);

            var loaded = await service.LoadTicketForContinuationAsync(first.SavedTicket!.Id);
            scale.SetManualWeightKg(17000m);
            await service.CaptureWeightAsync(loaded, 2);
            var second = await service.SaveAsync(loaded);

            Assert.True(second.Success);
            Assert.Equal(WeighTicketWorkflowState.Completed, second.WorkflowState);
            Assert.Equal(first.SavedTicket.Id, second.SavedTicket!.Id);
            Assert.Equal(first.SavedTicket.DisplayNumber, second.SavedTicket.DisplayNumber);
            Assert.Equal(2, second.SavedTicket.EventCount);
            Assert.Equal("—", second.SavedTicket.WorkflowStatusText);
            Assert.Equal(1000m, second.SavedTicket.NetWeightKg);
        });
    }

    [Fact]
    public async Task ReloadAfterWeigh1_CanContinueSameTicket()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);
            scale.SetManualWeightKg(18000m);

            var draft = new WeighTicketDraft();
            await service.CaptureWeightAsync(draft, 1);
            var first = await service.SaveAsync(draft);

            var reloaded = await service.LoadTicketForContinuationAsync(first.SavedTicket!.Id);
            Assert.Equal(first.SavedTicket.Id, reloaded.ExistingTicketId);
            Assert.Equal(first.SavedTicket.DisplayNumber, reloaded.DisplayNumber);
            Assert.True(reloaded.IsWeight1LockedFromSavedTicket);
        });
    }

    [Fact]
    public async Task TwoStage_DoesNotCreateDuplicateTickets()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft();
            scale.SetManualWeightKg(18000m);
            await service.CaptureWeightAsync(draft, 1);
            var first = await service.SaveAsync(draft);

            var loaded = await service.LoadTicketForContinuationAsync(first.SavedTicket!.Id);
            scale.SetManualWeightKg(17000m);
            await service.CaptureWeightAsync(loaded, 2);
            await service.SaveAsync(loaded);

            await using var scope = _factory.Provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
            Assert.Equal(1, await db.WeighTickets.CountAsync());
            Assert.Equal(2, await db.WeighEvents.CountAsync());
        });
    }

    [Fact]
    public async Task IndependentTimestamps_CanDifferByDay()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft();
            draft.SetWeightDraft(1, 18000m, new DateTimeOffset(2026, 6, 29, 23, 58, 0, TimeSpan.FromHours(7)));
            var first = await service.SaveAsync(draft);

            var loaded = await service.LoadTicketForContinuationAsync(first.SavedTicket!.Id);
            loaded.SetWeightDraft(2, 17000m, new DateTimeOffset(2026, 6, 30, 0, 12, 5, TimeSpan.FromHours(7)));
            await service.SaveAsync(loaded);

            var detail = await service.GetTicketDetailAsync(first.SavedTicket.Id);
            Assert.NotEqual(detail.Weight1RecordedAt?.Date, detail.Weight2RecordedAt?.Date);
        });
    }
}
