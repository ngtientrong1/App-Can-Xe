using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase13UsabilityTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private WeighTicketService CreateService() =>
        _factory.Provider.CreateScope().ServiceProvider.GetRequiredService<WeighTicketService>();

    private IScaleService CreateScale() =>
        _factory.Provider.GetRequiredService<IScaleService>();

    private CanXeDbContext CreateDb() =>
        _factory.Provider.CreateScope().ServiceProvider.GetRequiredService<CanXeDbContext>();

    [Fact]
    public async Task VehicleSearch_FindsPlateWithoutSeparators()
    {
        var scope = _factory.Provider.CreateScope();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        await vehicles.UpsertAsync("81C-017.28", null);

        var results = await vehicles.SearchAsync("81c01728");
        Assert.Contains(results, v => v.PlateNumber == "81C-017.28");
    }

    [Fact]
    public async Task VehicleSearch_ReturnsNormalizedPlateText()
    {
        var scope = _factory.Provider.CreateScope();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var vehicle = await vehicles.UpsertAsync("51C-123.45", null);

        Assert.Equal("51C-123.45", vehicle.PlateNumber);
    }

    [Fact]
    public async Task Save_OnlyWeight1_CreatesSingleEvent()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success);
        var db = CreateDb();
        Assert.Equal(1, await db.WeighEvents.CountAsync());
        Assert.Equal(8500m, save.SavedTicket!.SingleRecordedWeightKg);
        Assert.Null(save.SavedTicket.GrossWeightKg);
    }

    [Fact]
    public async Task Save_OnlyWeight2_CreatesSingleEvent()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(9200m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success);
        var db = CreateDb();
        Assert.Equal(1, await db.WeighEvents.CountAsync());
        var evt = await db.WeighEvents.SingleAsync();
        Assert.Equal(2, evt.Sequence);
        Assert.Equal(9200m, save.SavedTicket!.SingleRecordedWeightKg);
        Assert.Null(save.SavedTicket.GrossWeightKg);
    }

    [Fact]
    public async Task ListItem_SingleEvent_HasSingleRecordedWeight()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);

        var list = await service.GetFilteredAsync(new WeighTicketFilter());
        var item = Assert.Single(list);
        Assert.Equal(8500m, item.SingleRecordedWeightKg);
    }

    [Fact]
    public async Task ListItem_TwoEvents_HasNullSingleRecordedWeight()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);

        var list = await service.GetFilteredAsync(new WeighTicketFilter());
        var item = Assert.Single(list);
        Assert.Null(item.SingleRecordedWeightKg);
        Assert.Equal(10000m, item.NetWeightKg);
    }

    [Fact]
    public async Task ContinueSingleWeightTicket_UpdatesSameTicket()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        var first = await service.SaveAsync(draft);
        Assert.True(first.Success);

        var loaded = await service.LoadTicketForContinuationAsync(first.SavedTicket!.Id);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(loaded, 2);
        var second = await service.SaveAsync(loaded);

        Assert.Equal(first.SavedTicket.Id, second.SavedTicket!.Id);
        Assert.Null(second.SavedTicket.SingleRecordedWeightKg);
        Assert.Equal(10000m, second.SavedTicket.NetWeightKg);
    }

    [Fact]
    public async Task PreviewDisplayNumber_DoesNotIncrementOnCancel()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var previewBefore = await service.GetPreviewDisplayNumberAsync();

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        service.CancelDraft(draft);

        var previewAfter = await service.GetPreviewDisplayNumberAsync();
        Assert.Equal(previewBefore, previewAfter);
    }

    [Fact]
    public async Task Save_AssignsOfficialNumber_AndPreviewAdvances()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var preview = await service.GetPreviewDisplayNumberAsync();

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success);
        Assert.Equal(preview, save.SavedTicket!.DisplayNumber);

        var nextPreview = await service.GetPreviewDisplayNumberAsync();
        Assert.NotEqual(preview, nextPreview);
    }

    [Fact]
    public async Task ConcurrentSave_GeneratesUniqueDisplayNumbers()
    {
        var scale = CreateScale();
        scale.SetManualMode(true);

        async Task<string?> SaveOne(int weight)
        {
            using var scope = _factory.Provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
            var localScale = scope.ServiceProvider.GetRequiredService<IScaleService>();
            localScale.SetManualMode(true);
            localScale.SetManualWeightKg(weight);

            var draft = new WeighTicketDraft();
            await service.CaptureWeightAsync(draft, 1);
            var result = await service.SaveAsync(draft);
            return result.Success ? result.SavedTicket!.DisplayNumber : null;
        }

        var numbers = await Task.WhenAll(Enumerable.Range(1, 5).Select(i => SaveOne(8000 + i)));
        Assert.Equal(5, numbers.Distinct().Count());
    }

    [Fact]
    public void SingleRecordedWeightResolver_TwoEvents_ReturnsNull()
    {
        Assert.Null(SingleRecordedWeightResolver.Resolve(2, 8500000));
    }

    [Fact]
    public void SingleRecordedWeightResolver_OneEvent_ReturnsKg()
    {
        Assert.Equal(8500m, SingleRecordedWeightResolver.Resolve(1, 8500000));
    }
}
