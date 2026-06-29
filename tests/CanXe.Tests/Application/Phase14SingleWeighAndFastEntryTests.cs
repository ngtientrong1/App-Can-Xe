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
public class Phase14SingleWeighAndFastEntryTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<T> WithServiceAsync<T>(Func<WeighTicketService, Task<T>> action)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<WeighTicketService>());
    }

    private async Task WithServiceAsync(Func<WeighTicketService, Task> action)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<WeighTicketService>());
    }

    private IScaleService CreateScale() =>
        _factory.Provider.GetRequiredService<IScaleService>();

    [Fact]
    public async Task Save_SingleWeight_StoresGrossTareZeroNet()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);
            scale.SetManualWeightKg(11576m);

            var draft = new WeighTicketDraft();
            await service.CaptureWeightAsync(draft, 1);
            var save = await service.SaveAsync(draft);

            Assert.True(save.Success);
            Assert.Equal(11576m, save.SavedTicket!.GrossWeightKg);
            Assert.Equal(0m, save.SavedTicket.TareWeightKg);
            Assert.Equal(11576m, save.SavedTicket.NetWeightKg);
        });
    }

    [Fact]
    public async Task Save_SingleWeightWithPrice_ComputesBilling()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);
            scale.SetManualWeightKg(11576m);

            var draft = new WeighTicketDraft { DraftUnitPrice = 500m };
            await service.CaptureWeightAsync(draft, 1);
            var save = await service.SaveAsync(draft);

            Assert.True(save.Success);
            Assert.NotNull(save.SavedTicket!.BillableWeightKg);
            Assert.NotNull(save.SavedTicket.TotalAmountVnd);
        });
    }

    [Fact]
    public async Task ContinueSingleWeigh_AddsSecondWeight_RecalculatesDualFormula()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft();
            scale.SetManualWeightKg(11576m);
            await service.CaptureWeightAsync(draft, 1);
            var first = await service.SaveAsync(draft);
            Assert.Equal(11576m, first.SavedTicket!.NetWeightKg);

            var loaded = await service.LoadTicketForContinuationAsync(first.SavedTicket.Id);
            scale.SetManualWeightKg(8500m);
            await service.CaptureWeightAsync(loaded, 2);
            var second = await service.SaveAsync(loaded);

            Assert.Equal(first.SavedTicket.Id, second.SavedTicket!.Id);
            Assert.Equal(3076m, second.SavedTicket.NetWeightKg);
            Assert.Equal(8500m, second.SavedTicket.TareWeightKg);
        });
    }

    [Fact]
    public void ListItem_DoesNotExposeSingleRecordedWeightProperty()
    {
        Assert.Null(typeof(WeighTicketListItem).GetProperty("SingleRecordedWeightKg"));
    }

    [Fact]
    public async Task FilterSummary_IncludesSingleWeighNetInTotals()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft();
            scale.SetManualWeightKg(5000m);
            await service.CaptureWeightAsync(draft, 1);
            await service.SaveAsync(draft);

            var result = await service.GetFilteredWithSummaryAsync(new WeighTicketFilter());
            Assert.Equal(5000m, result.TotalNetWeightKg);
        });
    }

    [Fact]
    public async Task CaptureWeight1_BlockedAfterWeight2_WhenNoDevOverride()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft();
            scale.SetManualWeightKg(8500m);
            await service.CaptureWeightAsync(draft, 1);
            scale.SetManualWeightKg(18500m);
            await service.CaptureWeightAsync(draft, 2);

            scale.SetManualWeightKg(9999m);
            var attempt = await service.CaptureWeightAsync(draft, 1);

            Assert.False(attempt.Success);
            Assert.Equal(8500m, draft.DraftWeight1);
        });
    }

    [Fact]
    public async Task CaptureWeight1_AllowedWithDevOverride()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft { DeveloperWeight1OverrideEnabled = true };
            scale.SetManualWeightKg(8500m);
            await service.CaptureWeightAsync(draft, 1);
            scale.SetManualWeightKg(18500m);
            await service.CaptureWeightAsync(draft, 2);

            scale.SetManualWeightKg(8600m);
            var attempt = await service.CaptureWeightAsync(draft, 1);

            Assert.True(attempt.Success);
            Assert.Equal(8600m, draft.DraftWeight1);
        });
    }

    [Fact]
    public void DraftWorkflowRules_Weight1LockedWhenWeight2ExistsWithoutOverride()
    {
        Assert.False(DraftWorkflowRules.CanUpdateWeight1(false, true, false));
        Assert.True(DraftWorkflowRules.CanUpdateWeight1(false, true, true));
    }

    [Fact]
    public async Task VehicleSearch_NormalizedPlate_FindsFormattedPlate()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        await vehicles.UpsertAsync("81C-123.45", null);

        var results = await vehicles.SearchAsync("81c12345");
        Assert.Contains(results, v => v.PlateNumber == "81C-123.45");
    }

    [Fact]
    public async Task Filter_DateRange_IncludesFullEndDay()
    {
        await WithServiceAsync(async service =>
        {
            var scale = CreateScale();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft();
            scale.SetManualWeightKg(8500m);
            await service.CaptureWeightAsync(draft, 1);
            await service.SaveAsync(draft);

            var today = DateTimeOffset.Now.Date;
            var result = await service.GetFilteredWithSummaryAsync(new WeighTicketFilter
            {
                FromDate = today,
                ToDate = today.AddDays(1).AddTicks(-1)
            });

            Assert.Single(result.Items);
        });
    }

    [Fact]
    public void ActiveFilterChip_RemoveCustomer_KeepsOtherChips()
    {
        var filter = new WeighTicketFilter
        {
            CustomerName = "Chị Đức",
            CargoTypeName = "Cà tươi"
        };

        var chips = ActiveFilterChipBuilder.Build(filter);
        Assert.Equal(2, chips.Count);

        filter.CustomerName = null;
        var after = ActiveFilterChipBuilder.Build(filter);
        Assert.Single(after);
        Assert.Equal("cargo", after[0].Key);
    }

    [Fact]
    public void FilterSummary_SumsTotalAmountPerTicket()
    {
        var items = new[]
        {
            new WeighTicketListItem
            {
                DisplayNumber = "0001/06",
                TicketDateTime = DateTimeOffset.Now,
                NetWeightKg = 1000m,
                TotalAmountVnd = 100_000m
            },
            new WeighTicketListItem
            {
                DisplayNumber = "0002/06",
                TicketDateTime = DateTimeOffset.Now,
                NetWeightKg = 2000m,
                TotalAmountVnd = 300_000m
            }
        };

        var summary = FilterSummaryCalculator.Build(items);
        Assert.Equal(3000m, summary.TotalNetWeightKg);
        Assert.Equal(400_000m, summary.TotalAmountVnd);
    }
}
