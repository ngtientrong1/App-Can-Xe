using CanXe.Debt.Contract;
using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class ExtrasSplitAndDeleteTests
{
    private const string Name = "Thi Gia Chiêm";

    private static async Task Import(TestDb t, params ExportedTicket[] tickets)
    {
        var result = await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", tickets), "f.json", "u");
        Assert.True(result.Ok, result.Error);
    }

    private static async Task<List<int>> Ids(TestDb t, params string[] codes) =>
        await t.Db.Tickets.Where(x => codes.Contains(x.Code)).OrderBy(x => x.Code).Select(x => x.Id).ToListAsync();

    private static async Task<(Season Season, Customer Customer, SettlementPeriod Lot)> LotOfTwo(TestDb t)
    {
        var season = t.AddSeason();
        var customer = t.AddCustomer(Name);
        await Import(t, Make.Ticket("1", "2026-10-20", Name, amount: 1_000), Make.Ticket("2", "2026-10-21", Name, amount: 2_000));
        var lot = (await new SettlementService(t.Db).ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "1", "2"), "kt")).Value!;
        return (season, customer, lot);
    }

    // --- extra fees ---

    [Fact]
    public async Task ExtraFee_RaisesWhatTheLotHasToPay_AndTheCustomersBalance()
    {
        using var t = new TestDb();
        var (season, customer, lot) = await LotOfTwo(t);

        var added = await new PeriodExtraService(t.Db).AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2026, 10, 22), "Tiền xe", 250, "xe thuê", "kt");
        var view = (await new SettlementService(t.Db).GetAsync(customer.Id, season))!;

        Assert.True(added.Ok, added.Error);
        Assert.Equal(3_000, view.Periods[0].TicketsTotal);
        Assert.Equal(250, view.Periods[0].ExtrasTotal);
        Assert.Equal(3_250, view.Periods[0].Payable);
        Assert.Equal(3_250, view.Balance);
        Assert.Equal(3_250, view.Open.CarryIn);
        Assert.Equal(3_250, (await new LedgerService(t.Db).GetOverviewAsync(season)).TotalBalance);
    }

    [Fact]
    public async Task ExtraFee_CanBeNegative_AndPaymentsSettleItWithTheTrucks()
    {
        using var t = new TestDb();
        var (season, customer, lot) = await LotOfTwo(t);
        var extras = new PeriodExtraService(t.Db);
        await extras.AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2026, 10, 22), "Tiền bù hàng", 500, null, "kt");
        await extras.AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2026, 10, 22), "Trừ hao", -100, null, "kt");

        await new PaymentService(t.Db).AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 23), 3_400, PaymentMethod.Cash, null, "kt", periodId: lot.Id);
        var period = (await new SettlementService(t.Db).GetAsync(customer.Id, season))!.Periods[0];

        Assert.Equal(400, period.ExtrasTotal);
        Assert.Equal(PeriodStatus.Settled, period.Status);
    }

    [Fact]
    public async Task ExtraFee_NeedsAClosedLot_ALabel_AndAnAmount()
    {
        using var t = new TestDb();
        var (season, customer, lot) = await LotOfTwo(t);
        var extras = new PeriodExtraService(t.Db);

        Assert.False((await extras.AddAsync(customer.Id, season.Id, 0, new DateOnly(2026, 10, 22), "Tiền xe", 100, null, "kt")).Ok);
        Assert.False((await extras.AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2026, 10, 22), " ", 100, null, "kt")).Ok);
        Assert.False((await extras.AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2026, 10, 22), "Tiền xe", 0, null, "kt")).Ok);
        Assert.False((await extras.AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2020, 1, 1), "Tiền xe", 100, null, "kt")).Ok);
    }

    [Fact]
    public async Task VoidedExtraFee_NoLongerCounts_ButStaysOnRecord()
    {
        using var t = new TestDb();
        var (season, customer, lot) = await LotOfTwo(t);
        var extras = new PeriodExtraService(t.Db);
        var extra = (await extras.AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2026, 10, 22), "Tiền xe", 250, null, "kt")).Value!;

        Assert.False((await extras.VoidAsync(extra.Id, "", "kt")).Ok);
        Assert.True((await extras.VoidAsync(extra.Id, "Nhập nhầm", "kt")).Ok);

        var view = (await new SettlementService(t.Db).GetAsync(customer.Id, season))!;
        Assert.Equal(3_000, view.Balance);
        Assert.Empty(view.Periods[0].Extras);
        Assert.Equal("Nhập nhầm", (await t.Db.PeriodExtras.SingleAsync()).VoidReason);
    }

    [Fact]
    public async Task LotWithAnExtraFee_CannotBeUndoneUntilTheFeeIsVoided()
    {
        using var t = new TestDb();
        var (season, customer, lot) = await LotOfTwo(t);
        var extras = new PeriodExtraService(t.Db);
        var extra = (await extras.AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2026, 10, 22), "Tiền xe", 250, null, "kt")).Value!;
        var settlements = new SettlementService(t.Db);

        Assert.False((await settlements.UndoLastCloseAsync(customer.Id, season.Id)).Ok);
        await extras.VoidAsync(extra.Id, "Nhập nhầm", "kt");
        Assert.True((await settlements.UndoLastCloseAsync(customer.Id, season.Id)).Ok);
    }

    // --- cutting a truck ---

    private static async Task<(TestDb Db, TicketAdjustService Service, Customer Customer, Season Season, TicketRecord Truck)> OneBigTruck()
    {
        var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer(Name);
        await Import(t, Make.Ticket("big", "2026-10-20", Name, kg: 18_000m, price: 25_000m, amount: 448_650_000));
        return (t, new TicketAdjustService(t.Db), customer, season, await t.Db.Tickets.SingleAsync());
    }

    [Fact]
    public async Task Split_KeepsTheRestOnTheTruck_AndHoldsBackThePartWithoutAPrice()
    {
        var (t, service, customer, season, truck) = await OneBigTruck();
        using var _ = t;
        var lot = (await new SettlementService(t.Db).ClosePeriodAsync(customer.Id, season.Id, [truck.Id], "kt")).Value!;

        var held = await service.SplitAsync(truck.Id, 15_000m, null, "khách gửi lại kho", "kt");

        Assert.True(held.Ok, held.Error);
        var kept = await t.Db.Tickets.SingleAsync(x => x.Id == truck.Id);
        Assert.Equal(3_000m, kept.NetWeightKg);
        Assert.Equal(2_991m, kept.BillableWeightKg);
        Assert.Equal(2_991L * 25_000, kept.AmountVnd);
        Assert.Equal(lot.Id, kept.PeriodId);
        Assert.Equal(18_000m, kept.OriginalNetKg);

        var part = held.Value!;
        Assert.Equal(15_000m, part.NetWeightKg);
        Assert.Null(part.PeriodId);
        Assert.Null(part.AmountVnd);
        Assert.Equal(TicketStage.NoPrice, part.Stage);
        Assert.Equal(truck.Id, part.SplitFromTicketId);
        Assert.Contains("khách gửi lại kho", part.Notes);

        var view = (await new SettlementService(t.Db).GetAsync(customer.Id, season))!;
        Assert.Equal(2_991L * 25_000, view.Periods[0].TicketsTotal);
        Assert.Equal(1, view.Open.TruckCount);
        Assert.Equal(0, view.Open.TicketsTotal);
    }

    [Fact]
    public async Task Split_WithAPrice_PricesTheHeldPartOnItsOwn()
    {
        var (t, service, _, _, truck) = await OneBigTruck();
        using var _ = t;

        var held = (await service.SplitAsync(truck.Id, 15_000m, 26_000m, null, "kt")).Value!;

        Assert.Equal(14_955m, held.BillableWeightKg);
        Assert.Equal(14_955L * 26_000, held.AmountVnd);
        Assert.Equal(TicketStage.Priced, held.Stage);
    }

    [Fact]
    public async Task Split_SurvivesAReImportOfTheSameFile()
    {
        var (t, service, _, _, truck) = await OneBigTruck();
        using var _ = t;
        await service.SplitAsync(truck.Id, 15_000m, null, null, "kt");

        await Import(t, Make.Ticket("big", "2026-10-20", Name, kg: 18_000m, price: 25_000m, amount: 448_650_000));

        Assert.Equal(3_000m, (await t.Db.Tickets.SingleAsync(x => x.Code == "big")).NetWeightKg);
        Assert.Equal(2, await t.Db.Tickets.CountAsync(x => !x.IsDeleted));
    }

    [Fact]
    public async Task Split_RefusesWeightsThatDoNotFit()
    {
        var (t, service, _, _, truck) = await OneBigTruck();
        using var _ = t;

        Assert.False((await service.SplitAsync(truck.Id, 0m, null, null, "kt")).Ok);
        Assert.False((await service.SplitAsync(truck.Id, 18_000m, null, null, "kt")).Ok);
        Assert.False((await service.SplitAsync(truck.Id, 20_000m, null, null, "kt")).Ok);
        Assert.Single(t.Db.Tickets);
    }

    [Fact]
    public async Task Split_CanBeUndoneByMergingTheHeldPartBack()
    {
        var (t, service, _, _, truck) = await OneBigTruck();
        using var _ = t;
        var held = (await service.SplitAsync(truck.Id, 15_000m, null, null, "kt")).Value!;

        Assert.False((await service.RestoreAsync(truck.Id, "kt")).Ok);
        var merged = await service.MergeBackAsync(held.Id, "kt");

        Assert.True(merged.Ok, merged.Error);
        var truckNow = await t.Db.Tickets.SingleAsync(x => x.Id == truck.Id);
        Assert.Equal(18_000m, truckNow.NetWeightKg);
        Assert.Equal(448_650_000, truckNow.AmountVnd);
        Assert.False(truckNow.IsAdjusted);
        Assert.True((await t.Db.Tickets.SingleAsync(x => x.Id == held.Id)).IsDeleted);
    }

    [Fact]
    public async Task HeldPart_CanBeSoldLater_InALotOfItsOwn()
    {
        var (t, service, customer, season, truck) = await OneBigTruck();
        using var _ = t;
        var settlements = new SettlementService(t.Db);
        await settlements.ClosePeriodAsync(customer.Id, season.Id, [truck.Id], "kt");
        var held = (await service.SplitAsync(truck.Id, 15_000m, null, null, "kt")).Value!;

        await service.AdjustAsync(held.Id, 15_000m, 27_000m, "Giá lúc bán", "kt");
        var second = await settlements.ClosePeriodAsync(customer.Id, season.Id, [held.Id], "kt");

        Assert.True(second.Ok, second.Error);
        var view = (await settlements.GetAsync(customer.Id, season))!;
        Assert.Equal(14_955L * 27_000, view.Periods[1].TicketsTotal);
        Assert.False((await service.MergeBackAsync(held.Id, "kt")).Ok);
    }

    // --- deleting a customer ---

    [Fact]
    public async Task DeleteCustomer_RemovesTheirDebtRecords_AndLaterTicketsComeBackForADecision()
    {
        using var t = new TestDb();
        var (season, customer, lot) = await LotOfTwo(t);
        await new PaymentService(t.Db).AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 23), 500, PaymentMethod.Cash, null, "kt", periodId: lot.Id);
        await new PeriodExtraService(t.Db).AddAsync(customer.Id, season.Id, lot.Id, new DateOnly(2026, 10, 22), "Tiền xe", 250, null, "kt");
        var customers = new CustomerService(t.Db);

        var result = await customers.DeleteAsync(customer.Id);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(2, result.Value!.Tickets);
        Assert.Equal(1, result.Value.Payments);
        Assert.Empty(t.Db.Customers);
        Assert.Empty(t.Db.Payments);
        Assert.Empty(t.Db.Periods);
        Assert.Empty(t.Db.PeriodExtras);
        Assert.All(t.Db.Tickets, x => Assert.True(x.IsDeleted));
        Assert.Empty(await customers.GetUnmappedNamesAsync());
        Assert.Equal(0, (await new LedgerService(t.Db).GetOverviewAsync(season)).TotalBilled);

        await Import(t, Make.Ticket("3", "2026-10-25", Name, amount: 700));
        var names = await customers.GetUnmappedNamesAsync();
        Assert.Equal(Name, Assert.Single(names).DisplayName);
        Assert.Equal(1, (await new LedgerService(t.Db).GetOverviewAsync(season)).UnmappedTickets);
    }

    [Fact]
    public async Task DeleteCustomer_IsRefusedWhenTheirDataIsInAClosedSeason()
    {
        using var t = new TestDb();
        var customer = t.AddCustomer(Name);
        var season = t.AddSeason();
        await Import(t, Make.Ticket("1", "2026-10-20", Name, amount: 1_000));
        season.IsClosed = true;
        await t.Db.SaveChangesAsync();

        var result = await new CustomerService(t.Db).DeleteAsync(customer.Id);

        Assert.False(result.Ok);
        Assert.Single(t.Db.Customers);
    }

    [Fact]
    public async Task DismissName_DropsTheUnmatchedTickets_UntilNewOnesArrive()
    {
        using var t = new TestDb();
        t.AddSeason();
        await Import(t, Make.Ticket("1", "2026-10-20", "Khách Lẻ", amount: 100), Make.Ticket("2", "2026-10-21", "Khách Lẻ", amount: 200));
        var customers = new CustomerService(t.Db);
        var normalized = NameNormalizer.Normalize("Khách Lẻ");

        Assert.Equal(2, await customers.DismissNameAsync(normalized));
        Assert.Empty(await customers.GetUnmappedNamesAsync());

        await Import(t, Make.Ticket("3", "2026-10-22", "Khách Lẻ", amount: 300));
        Assert.Equal(1, Assert.Single(await customers.GetUnmappedNamesAsync()).TicketCount);
    }

    [Fact]
    public async Task SchemaUpgrader_AddsTheExtrasTableAndSplitColumn_ToAnOlderDatabase()
    {
        using var t = new TestDb();
        await t.Db.Database.ExecuteSqlRawAsync("DROP TABLE \"PeriodExtras\"");
        await t.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Tickets\" DROP COLUMN \"SplitFromTicketId\"");

        var pending = await SchemaUpgrader.PendingChangesAsync(t.Db);
        await SchemaUpgrader.ApplyAsync(t.Db);

        Assert.Contains("PeriodExtras", pending);
        Assert.Contains("Tickets.SplitFromTicketId", pending);
        Assert.Empty(await SchemaUpgrader.PendingChangesAsync(t.Db));
        Assert.Empty(await t.Db.PeriodExtras.ToListAsync());
    }
}
