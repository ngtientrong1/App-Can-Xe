using CanXe.Debt.Contract;
using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class TicketPricingTests
{
    [Theory]
    [InlineData(12000, 11964)]
    [InlineData(12050, 12014)]
    [InlineData(1500, 1496)]
    public void BillableWeight_LosesThreePerMille_RoundedHalfUp(decimal net, decimal expected) =>
        Assert.Equal(expected, TicketPricing.BillableOf(net));

    [Fact]
    public void TwoLevelPrice_DeductsFromEachPartAndPricesEachOnItsOwn()
    {
        var (billable, amount) = TicketPricing.Price(10000m, 25000m, 4000m, 26000m);

        Assert.Equal(3988m + 5982m, billable);
        Assert.Equal(3988L * 25000 + 5982L * 26000, amount);
    }
}

public sealed class TicketAdjustTests
{
    private static async Task<(TestDb Db, TicketAdjustService Service, TicketRecord Ticket)> OneTicket(bool closedSeason = false)
    {
        var t = new TestDb();
        t.AddSeason(closed: closedSeason);
        t.AddCustomer("Công Ty Đại Lợi");
        await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", Make.Ticket("a", kg: 12000m, price: 25900m, amount: 309_867_600)), "f.json", "ketoan");
        return (t, new TicketAdjustService(t.Db), await t.Db.Tickets.SingleAsync());
    }

    [Fact]
    public async Task Adjust_PaysTheCustomersWarehouseWeight_AndKeepsOurOwnNumber()
    {
        var (t, service, ticket) = await OneTicket();
        using var _ = t;

        var result = await service.AdjustAsync(ticket.Id, 12050m, null, "Lấy theo cân kho khách", "admin");

        Assert.True(result.Ok, result.Error);
        var saved = await t.Db.Tickets.SingleAsync();
        Assert.Equal(12050m, saved.NetWeightKg);
        Assert.Equal(12014m, saved.BillableWeightKg);
        Assert.Equal(12014L * 25900, saved.AmountVnd);
        Assert.Equal(12000m, saved.OriginalNetKg);
        Assert.Equal(309_867_600, saved.OriginalAmountVnd);
        Assert.True(saved.IsAdjusted);
        Assert.Contains("12.000 → 12.050", saved.AdjustLog);
        Assert.Contains("Lấy theo cân kho khách", saved.AdjustLog);
    }

    [Fact]
    public async Task Adjust_NeedsAReason_AndRealNumbers()
    {
        var (t, service, ticket) = await OneTicket();
        using var _ = t;

        Assert.False((await service.AdjustAsync(ticket.Id, 12050m, null, "  ", "admin")).Ok);
        Assert.False((await service.AdjustAsync(ticket.Id, 0m, null, "x", "admin")).Ok);
        Assert.False((await service.AdjustAsync(ticket.Id, 12000m, null, "x", "admin")).Ok);
        Assert.False((await t.Db.Tickets.SingleAsync()).IsAdjusted);
    }

    [Fact]
    public async Task Adjust_CanAlsoFixTheUnitPrice()
    {
        var (t, service, ticket) = await OneTicket();
        using var _ = t;

        await service.AdjustAsync(ticket.Id, 12000m, 26000m, "Chốt lại giá", "admin");

        var saved = await t.Db.Tickets.SingleAsync();
        Assert.Equal(26000m, saved.UnitPrice);
        Assert.Equal(11964L * 26000, saved.AmountVnd);
        Assert.Equal(25900m, saved.OriginalUnitPrice);
    }

    [Fact]
    public async Task ReImportingTheFile_KeepsTheCorrection_ButTakesTheNewPriceFromTheScale()
    {
        var (t, service, ticket) = await OneTicket();
        using var _ = t;
        await service.AdjustAsync(ticket.Id, 12050m, null, "Lấy theo cân kho khách", "admin");

        var same = await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", Make.Ticket("a", kg: 12000m, price: 25900m, amount: 309_867_600)), "f.json", "ketoan");
        Assert.Equal(12050m, (await t.Db.Tickets.SingleAsync()).NetWeightKg);
        Assert.Equal(1, same.Value!.Unchanged);

        await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", Make.Ticket("a", kg: 12000m, price: 26000m, amount: 311_064_000)), "f.json", "ketoan");

        var saved = await t.Db.Tickets.SingleAsync();
        Assert.Equal(12050m, saved.NetWeightKg);
        Assert.Equal(26000m, saved.UnitPrice);
        Assert.Equal(12014L * 26000, saved.AmountVnd);
        Assert.Equal(26000m, saved.OriginalUnitPrice);
    }

    [Fact]
    public async Task Restore_GoesBackToExactlyWhatTheScaleSent()
    {
        var (t, service, ticket) = await OneTicket();
        using var _ = t;
        await service.AdjustAsync(ticket.Id, 12050m, null, "Lấy theo cân kho khách", "admin");

        var result = await service.RestoreAsync(ticket.Id, "admin");

        Assert.True(result.Ok, result.Error);
        var saved = await t.Db.Tickets.SingleAsync();
        Assert.False(saved.IsAdjusted);
        Assert.Equal(12000m, saved.NetWeightKg);
        Assert.Equal(309_867_600, saved.AmountVnd);
        Assert.Contains("Khôi phục", saved.AdjustLog);
        Assert.False((await service.RestoreAsync(ticket.Id, "admin")).Ok);
    }

    [Fact]
    public async Task Adjust_IsRefusedInAClosedSeason_AndWhileWaitingForTheSecondWeighing()
    {
        var (t, service, ticket) = await OneTicket();
        using var _ = t;
        var season = await t.Db.Seasons.SingleAsync();
        season.IsClosed = true;
        await t.Db.SaveChangesAsync();

        Assert.False((await service.AdjustAsync(ticket.Id, 12050m, null, "x", "admin")).Ok);

        season.IsClosed = false;
        (await t.Db.Tickets.SingleAsync()).AwaitingSecondWeigh = true;
        await t.Db.SaveChangesAsync();
        Assert.False((await service.AdjustAsync(ticket.Id, 12050m, null, "x", "admin")).Ok);
    }
}