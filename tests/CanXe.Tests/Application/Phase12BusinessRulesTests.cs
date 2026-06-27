using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase12BusinessRulesTests : IAsyncLifetime
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
    public async Task Save_TwoWeightsWithoutUnitPrice_StoresGrossTareNetButNullBilling()
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
        Assert.Equal(18500m, save.SavedTicket!.GrossWeightKg);
        Assert.Equal(8500m, save.SavedTicket.TareWeightKg);
        Assert.Equal(10000m, save.SavedTicket.NetWeightKg);
        Assert.Null(save.SavedTicket.BillableWeightKg);
        Assert.Null(save.SavedTicket.TotalAmountVnd);

        var db = CreateDb();
        var ticket = await db.WeighTickets.SingleAsync();
        Assert.NotNull(ticket.GrossWeightGrams);
        Assert.Null(ticket.DeductionWeightGrams);
        Assert.Null(ticket.BillableWeightGrams);
        Assert.Null(ticket.TotalAmountVnd);
    }

    [Fact]
    public async Task Save_WithPositiveUnitPrice_ComputesFullBilling()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftUnitPrice = 500m };
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        Assert.Equal(9970m, save.SavedTicket!.BillableWeightKg);
        Assert.Equal(4_985_000m, save.SavedTicket.TotalAmountVnd);
    }

    [Fact]
    public void Draft_ClearingUnitPrice_ClearsBillingOnRecalculate()
    {
        var draft = new WeighTicketDraft
        {
            DraftWeight1 = 8500m,
            DraftWeight2 = 18500m,
            DraftUnitPrice = 500m
        };

        var withPrice = WeightCalculator.Calculate(draft.DraftWeight1, draft.DraftWeight2, draft.DraftUnitPrice);
        Assert.NotNull(withPrice.BillableWeightKg);

        draft.DraftUnitPrice = null;
        var withoutPrice = WeightCalculator.Calculate(draft.DraftWeight1, draft.DraftWeight2, draft.DraftUnitPrice);
        Assert.Equal(10000m, withoutPrice.NetWeightKg);
        Assert.Null(withoutPrice.DeductionWeightKg);
        Assert.Null(withoutPrice.BillableWeightKg);
        Assert.Null(withoutPrice.TotalAmountVnd);
    }

    [Fact]
    public async Task Save_ZeroUnitPrice_StoredAsNullBilling()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftUnitPrice = 0m };
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        Assert.Null(save.SavedTicket!.BillableWeightKg);
        Assert.Null(save.SavedTicket.TotalAmountVnd);
        Assert.Equal(10000m, save.SavedTicket.NetWeightKg);

        var db = CreateDb();
        var ticket = await db.WeighTickets.SingleAsync();
        Assert.Null(ticket.UnitPriceVndPerKg);
        Assert.Null(ticket.BillableWeightGrams);
    }

    [Fact]
    public async Task ListItem_ExcludesWeightColumns_DetailIncludesThem()
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
        Assert.Null(item.GetType().GetProperty("Weight1Kg"));
        Assert.Null(item.GetType().GetProperty("Weight2Kg"));

        var detail = await service.GetTicketDetailAsync(item.Id);
        Assert.Equal(8500m, detail.Weight1Kg);
        Assert.Equal(18500m, detail.Weight2Kg);
    }

    [Fact]
    public async Task Save_SingleWeight_GrossTareNetNull()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        Assert.Null(save.SavedTicket!.GrossWeightKg);
        Assert.Null(save.SavedTicket.TareWeightKg);
        Assert.Null(save.SavedTicket.NetWeightKg);
    }

    [Fact]
    public async Task Filter_ByUnitPrice_ExcludesServiceWeighTickets()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var serviceDraft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(serviceDraft, 1);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(serviceDraft, 2);
        var serviceSave = await service.SaveAsync(serviceDraft);
        Assert.True(serviceSave.Success);

        var billedDraft = new WeighTicketDraft { DraftUnitPrice = 500m };
        scale.SetManualWeightKg(9000m);
        await service.CaptureWeightAsync(billedDraft, 1);
        scale.SetManualWeightKg(19000m);
        await service.CaptureWeightAsync(billedDraft, 2);
        var billedSave = await service.SaveAsync(billedDraft);
        Assert.True(billedSave.Success);

        var byPrice = await service.GetFilteredAsync(new WeighTicketFilter { UnitPriceVndPerKg = 500m });
        Assert.Single(byPrice);
        Assert.Equal(billedSave.SavedTicket!.Id, byPrice[0].Id);
    }
}
