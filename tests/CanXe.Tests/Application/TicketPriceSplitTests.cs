using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class TicketPriceSplitTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static async Task<WeighTicketDraft> TwoWeighDraftAsync(IServiceProvider provider)
    {
        var service = provider.GetRequiredService<WeighTicketService>();
        var scale = provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = "Công Ty Đại Lợi",
            DraftCargoType = "Cà tươi",
            DraftVehicle = "51C-123.45",
            DraftUnitPrice = 19800m
        };
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(21070m);
        await service.CaptureWeightAsync(draft, 2);
        return draft;
    }

    [Fact]
    public async Task SavingASplitTicket_StoresBothPricesAndTheCombinedAmount()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = await TwoWeighDraftAsync(scope.ServiceProvider);
        draft.DraftSplitFirstWeightKg = 15000m;
        draft.DraftSecondUnitPrice = 19600m;

        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(15000m, save.SavedTicket!.SplitFirstWeightKg);
        Assert.Equal(19600m, save.SavedTicket.SecondUnitPriceVndPerKg);
        Assert.Equal("19.800 / 19.600".Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator),
            save.SavedTicket.UnitPriceDisplay);

        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        var ticket = await db.WeighTickets.AsNoTracking().SingleAsync();
        Assert.Equal(16022000, ticket.BillableWeightGrams);
        Assert.Equal(15000m * 19800m + 1022m * 19600m, (decimal)ticket.TotalAmountVnd!.Value);
        Assert.Equal(15_000_000, ticket.SplitFirstWeightGrams);
        Assert.Equal(19600, ticket.SecondUnitPriceVndPerKg);
        Assert.Equal(19800, ticket.UnitPriceVndPerKg);
    }

    [Fact]
    public async Task SingleTicket_IsUnchangedAndHasNoSplit()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = await TwoWeighDraftAsync(scope.ServiceProvider);

        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        Assert.False(save.SavedTicket!.HasPriceSplit);

        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        var ticket = await db.WeighTickets.AsNoTracking().SingleAsync();
        Assert.Null(ticket.SplitFirstWeightGrams);
        Assert.Null(ticket.SecondUnitPriceVndPerKg);
        Assert.Equal(16022m * 19800m, (decimal)ticket.TotalAmountVnd!.Value);
    }

    [Theory]
    [InlineData(16022)]
    [InlineData(20000)]
    public async Task FirstPartNotSmallerThanBillableWeight_IsRejected(int firstKg)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = await TwoWeighDraftAsync(scope.ServiceProvider);
        draft.DraftSplitFirstWeightKg = firstKg;
        draft.DraftSecondUnitPrice = 19600m;

        var save = await service.SaveAsync(draft);

        Assert.False(save.Success);
        Assert.Contains("phải nhỏ hơn KL tính tiền", save.ErrorMessage);
    }

    [Fact]
    public async Task HalfFilledSplit_IsRejected()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = await TwoWeighDraftAsync(scope.ServiceProvider);
        draft.DraftSplitFirstWeightKg = 15000m;

        var save = await service.SaveAsync(draft);

        Assert.False(save.Success);
        Assert.Contains("nhập đủ", save.ErrorMessage);
    }

    [Fact]
    public async Task EditingATicket_CanAddChangeAndRemoveTheSplit()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var updates = scope.ServiceProvider.GetRequiredService<TicketUpdateService>();
        var draft = await TwoWeighDraftAsync(scope.ServiceProvider);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        var id = save.SavedTicket!.Id;

        var edit = await updates.LoadForEditAsync(id);
        edit.DraftSplitFirstWeightKg = 15000m;
        edit.DraftSecondUnitPrice = 19600m;
        var added = await updates.UpdateAsync(edit, "admin");
        Assert.True(added.Success, added.ErrorMessage);
        Assert.True(added.UpdatedTicket!.HasPriceSplit);
        Assert.Equal(15000m * 19800m + 1022m * 19600m, added.UpdatedTicket.TotalAmountVnd);

        var again = await updates.LoadForEditAsync(id);
        Assert.Equal(15000m, again.DraftSplitFirstWeightKg);
        again.DraftSplitFirstWeightKg = null;
        again.DraftSecondUnitPrice = null;
        var removed = await updates.UpdateAsync(again, "admin");
        Assert.True(removed.Success, removed.ErrorMessage);
        Assert.False(removed.UpdatedTicket!.HasPriceSplit);
        Assert.Equal(16022m * 19800m, removed.UpdatedTicket.TotalAmountVnd);
    }
}
