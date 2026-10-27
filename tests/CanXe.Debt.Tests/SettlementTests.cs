using CanXe.Debt.Contract;
using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class PeriodBuilderTests
{
    private static readonly Dictionary<string, string> Stations = new() { ["DAKHA"] = "Đắk Hà", ["DAKTO"] = "Đắk Tô" };
    private static readonly Season Season = new() { Id = 1, Name = "Mùa cà", StartDate = new DateOnly(2025, 11, 1) };

    private static TicketRecord Ticket(
        string code, string date, long? amount, string station = "DAKHA", bool awaiting = false, decimal kg = 1000m,
        DateTimeOffset? updatedAt = null, int? periodId = null, string time = "08:00:00") =>
        new()
        {
            StationCode = station,
            Code = code,
            DisplayNumber = code,
            TicketDate = DateOnly.Parse(date),
            TicketDateTime = DateTimeOffset.Parse($"{date}T{time}+07:00"),
            BillableWeightKg = awaiting || amount is null ? null : kg,
            NetWeightKg = awaiting ? null : kg,
            AmountVnd = awaiting ? null : amount,
            AwaitingSecondWeigh = awaiting,
            PeriodId = periodId,
            ContentHash = "x",
            UpdatedAt = updatedAt ?? DateTimeOffset.Parse($"{date}T09:00:00+07:00")
        };

    private static SettlementPeriod Period(int id, string closedAt) =>
        new()
        {
            Id = id, CustomerId = 1, SeasonId = 1, FromDate = new DateOnly(2025, 11, 1), ToDate = new DateOnly(2025, 11, 1),
            ClosedAt = DateTimeOffset.Parse($"{closedAt}T18:00:00+07:00"), ClosedBy = "ketoan"
        };

    private static Payment Pay(string date, long amount, int? periodId) =>
        new() { Id = (int)amount % 1000, CustomerId = 1, SeasonId = 1, PeriodId = periodId, PaidOn = DateOnly.Parse(date), AmountVnd = amount, CreatedBy = "ketoan" };

    [Fact]
    public void RemainderOfAPartlyPaidPeriodCarriesIntoTheNext_LikeTheOwnersSheet()
    {
        var tickets = new[]
        {
            Ticket("a", "2025-11-03", 434_233_380, periodId: 1), Ticket("b", "2025-11-04", 441_184_464, periodId: 1),
            Ticket("c", "2025-11-05", 286_418_160, periodId: 1), Ticket("d", "2025-11-05", 405_005_328, periodId: 1),
            Ticket("e", "2025-11-05", 433_144_656, periodId: 1),
            Ticket("f", "2025-11-24", 814_469_240), Ticket("g", "2025-11-25", 784_658_940)
        };
        var payments = new[] { Pay("2025-11-08", 500_000_000, 1), Pay("2025-11-12", 600_000_000, 1) };

        var result = PeriodBuilder.Build(Season, tickets, [Period(1, "2025-11-06")], payments, Stations);

        var first = result.Periods[0];
        Assert.Equal(1_999_985_988, first.TicketsTotal);
        Assert.Equal(0, first.CarryIn);
        Assert.Equal(1_100_000_000, first.Paid);
        Assert.Equal(899_985_988, first.Remaining);
        Assert.Equal(PeriodStatus.Owing, first.Status);
        Assert.Equal(1, first.Number);

        var open = result.Open;
        Assert.True(open.IsOpen);
        Assert.Equal(0, open.Number);
        Assert.Equal(899_985_988, open.CarryIn);
        Assert.Equal(1_599_128_180, open.TicketsTotal);
        Assert.Equal(2_499_114_168, open.Payable);
        Assert.Equal(result.Balance, open.Remaining);
    }

    [Fact]
    public void ThreeTrucksOfTheSameDay_OneCanBePaidAheadWhileTheOthersWait()
    {
        // "3 xe 3 phiếu nhưng trả trước 1 xe": the first truck is its own period, paid in full.
        var tickets = new[]
        {
            Ticket("57/10", "2025-11-05", 400, time: "06:38:00", periodId: 1),
            Ticket("58/10", "2025-11-05", 500, time: "09:10:00"),
            Ticket("59/10", "2025-11-05", 600, time: "14:20:00")
        };

        var result = PeriodBuilder.Build(Season, tickets, [Period(1, "2025-11-05")], [Pay("2025-11-05", 400, 1)], Stations);

        Assert.Equal(PeriodStatus.Settled, result.Periods[0].Status);
        Assert.Equal(1, result.Periods[0].TruckCount);
        Assert.Equal(2, result.Open.TruckCount);
        Assert.Equal(1_100, result.Open.Payable);
        Assert.Equal(1_100, result.Balance);
    }

    [Fact]
    public void PeriodIsALooseRangeOfTickets_FirstAndLastAreByWeighingTime()
    {
        var tickets = new[]
        {
            Ticket("C", "2025-11-06", 1, time: "15:00:00", periodId: 1),
            Ticket("A", "2025-11-05", 1, time: "07:00:00", periodId: 1),
            Ticket("B", "2025-11-05", 1, time: "11:00:00", periodId: 1)
        };

        var period = PeriodBuilder.Build(Season, tickets, [Period(1, "2025-11-07")], [], Stations).Periods[0];

        Assert.Equal("A", period.FirstTicket!.Code);
        Assert.Equal("C", period.LastTicket!.Code);
        Assert.Equal(new DateOnly(2025, 11, 5), period.From);
        Assert.Equal(new DateOnly(2025, 11, 6), period.To);
    }

    [Fact]
    public void PayingAPeriodInFullSettlesIt_AndNothingCarries()
    {
        var result = PeriodBuilder.Build(
            Season, [Ticket("a", "2025-11-03", 1_000, periodId: 1)], [Period(1, "2025-11-06")], [Pay("2025-11-08", 1_000, 1)], Stations);

        Assert.Equal(PeriodStatus.Settled, result.Periods[0].Status);
        Assert.Equal(0, result.Open.CarryIn);
    }

    [Fact]
    public void PayingMoreThanThePeriod_IsPaidAheadAndReducesTheNextPeriod()
    {
        var tickets = new[] { Ticket("a", "2025-11-03", 1_000, periodId: 1), Ticket("b", "2025-11-10", 400) };

        var result = PeriodBuilder.Build(Season, tickets, [Period(1, "2025-11-06")], [Pay("2025-11-08", 1_300, 1)], Stations);

        Assert.Equal(PeriodStatus.PaidAhead, result.Periods[0].Status);
        Assert.Equal(-300, result.Periods[0].Remaining);
        Assert.Equal(-300, result.Open.CarryIn);
        Assert.Equal(100, result.Open.Remaining);
    }

    [Fact]
    public void PaymentsWithoutAPeriodCountTowardTheOpenPool()
    {
        var result = PeriodBuilder.Build(
            Season, [Ticket("a", "2025-11-03", 1_000)], [], [Pay("2025-11-08", 400, null)], Stations);

        Assert.Single(result.Periods);
        Assert.Equal(400, result.Open.Paid);
        Assert.Equal(600, result.Open.Remaining);
    }

    [Fact]
    public void TrucksWaitingForWeightOrPriceAreCountedButNotAddedToTheTotal()
    {
        var tickets = new[]
        {
            Ticket("a", "2025-11-03", 1_000),
            Ticket("b", "2025-11-03", null),
            Ticket("c", "2025-11-03", null, awaiting: true)
        };

        var open = PeriodBuilder.Build(Season, tickets, [], [], Stations).Open;

        Assert.Equal(3, open.TruckCount);
        Assert.Equal(1, open.PricedCount);
        Assert.Equal(1, open.NoPriceCount);
        Assert.Equal(1, open.AwaitingCount);
        Assert.Equal(1_000, open.TicketsTotal);
        Assert.True(open.WaitingForWeightOrPrice);
    }

    [Fact]
    public void EachStationGetsItsOwnSubtotal()
    {
        var tickets = new[]
        {
            Ticket("a", "2025-11-03", 300, "DAKHA", kg: 100), Ticket("b", "2025-11-03", 200, "DAKHA", kg: 50),
            Ticket("c", "2025-11-03", 500, "DAKTO", kg: 80)
        };

        var stations = PeriodBuilder.Build(Season, tickets, [], [], Stations).Open.ByStation;

        Assert.Equal(["Đắk Hà", "Đắk Tô"], stations.Select(s => s.StationName));
        Assert.Equal([2, 1], stations.Select(s => s.TruckCount));
        Assert.Equal([500L, 500L], stations.Select(s => s.Amount));
        Assert.Equal(150, stations[0].BillableKg);
    }

    [Fact]
    public void TrucksChangedAfterThePeriodWasClosedAreFlagged()
    {
        var tickets = new[]
        {
            Ticket("a", "2025-11-03", 1_000, periodId: 1),
            Ticket("late", "2025-11-04", 700, updatedAt: DateTimeOffset.Parse("2025-11-09T10:00:00+07:00"), periodId: 1)
        };

        var view = PeriodBuilder.Build(Season, tickets, [Period(1, "2025-11-06")], [], Stations).Periods[0];

        Assert.Equal(1, view.ChangedAfterCloseCount);
        Assert.Equal(700, view.ChangedAfterCloseAmount);
        Assert.Equal(1_700, view.TicketsTotal);
    }

    [Fact]
    public void LastRemainderAlwaysEqualsBilledMinusPaid()
    {
        var periods = new[] { Period(1, "2025-11-06"), Period(2, "2025-11-11") };
        var tickets = new[]
        {
            Ticket("a", "2025-11-03", 900, periodId: 1), Ticket("b", "2025-11-08", 800, periodId: 2), Ticket("c", "2025-11-20", 700)
        };
        var payments = new[] { Pay("2025-11-07", 500, 1), Pay("2025-11-12", 1_000, 2), Pay("2025-11-13", 100, null) };

        var result = PeriodBuilder.Build(Season, tickets, periods, payments, Stations);

        Assert.Equal(2_400, result.TotalBilled);
        Assert.Equal(1_600, result.TotalPaid);
        Assert.Equal(result.Balance, result.Open.Remaining);
    }
}

public sealed class SettlementServiceTests
{
    private static async Task Import(TestDb t, string station, params ExportedTicket[] tickets)
    {
        var result = await new ImportService(t.Db).ImportAsync(Make.File(station, tickets), "f.json", "u");
        Assert.True(result.Ok, result.Error);
    }

    private static (Season Season, Customer Customer) Setup(TestDb t) => (t.AddSeason(), t.AddCustomer("Thi Gia Chiêm"));

    private static async Task<List<int>> Ids(TestDb t, params string[] codes) =>
        await t.Db.Tickets.Where(x => codes.Contains(x.Code)).OrderBy(x => x.Code).Select(x => x.Id).ToListAsync();

    [Fact]
    public async Task ClosingGathersTheChosenTrucks_AndTheRestKeepWaiting()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA",
            Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 100), Make.Ticket("2", "2026-10-09", "Thi Gia Chiêm", amount: 200),
            Make.Ticket("3", "2026-10-09", "Thi Gia Chiêm", amount: 300));
        var service = new SettlementService(t.Db);

        var closed = await service.ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "1", "2"), "ketoan");
        var settlement = (await service.GetAsync(customer.Id, season))!;

        Assert.True(closed.Ok, closed.Error);
        Assert.Equal(new DateOnly(2026, 10, 3), closed.Value!.FromDate);
        Assert.Equal(new DateOnly(2026, 10, 9), closed.Value.ToDate);
        Assert.Equal(300, settlement.Periods[0].TicketsTotal);
        Assert.Equal(1, settlement.Open.TruckCount);
        Assert.Equal(300, settlement.Open.TicketsTotal);
    }

    [Fact]
    public async Task OneTruckOfThreeInTheSameDay_CanBeClosedAndPaidOnItsOwn()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA",
            Make.Ticket("57/10", "2026-10-27", "Thi Gia Chiêm", amount: 400), Make.Ticket("58/10", "2026-10-27", "Thi Gia Chiêm", amount: 500),
            Make.Ticket("59/10", "2026-10-27", "Thi Gia Chiêm", amount: 600));
        var service = new SettlementService(t.Db);
        var period = (await service.ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "57/10"), "ketoan")).Value!;

        await new PaymentService(t.Db).AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 27), 400, PaymentMethod.Cash, null, "ketoan", periodId: period.Id);
        var settlement = (await service.GetAsync(customer.Id, season))!;

        Assert.Equal(PeriodStatus.Settled, settlement.Periods[0].Status);
        Assert.Equal(2, settlement.Open.TruckCount);
        Assert.Equal(1_100, settlement.Open.Remaining);
    }

    [Fact]
    public async Task ClosingIsRefusedForBadSelections()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        var other = t.AddCustomer("Nguyễn Văn An");
        await Import(t, "DAKHA",
            Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 100), Make.Ticket("2", "2026-10-04", "Thi Gia Chiêm", amount: 200),
            Make.Ticket("9", "2026-10-04", "Nguyễn Văn An", amount: 900));
        var service = new SettlementService(t.Db);
        var one = await Ids(t, "1");
        await service.ClosePeriodAsync(customer.Id, season.Id, one, "ketoan");

        Assert.False((await service.ClosePeriodAsync(customer.Id, season.Id, [], "ketoan")).Ok);
        Assert.False((await service.ClosePeriodAsync(customer.Id, season.Id, one, "ketoan")).Ok);                    // already in a period
        Assert.False((await service.ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "9"), "ketoan")).Ok);     // someone else's truck
        Assert.False((await service.ClosePeriodAsync(customer.Id, season.Id, [99999], "ketoan")).Ok);               // unknown
        Assert.True((await service.ClosePeriodAsync(other.Id, season.Id, await Ids(t, "9"), "ketoan")).Ok);
    }

    [Fact]
    public async Task ClosedSeasonRefusesNewPeriods()
    {
        using var t = new TestDb();
        var season = t.AddSeason(end: "2026-12-31", closed: true);
        var customer = t.AddCustomer("Thi Gia Chiêm");

        var result = await new SettlementService(t.Db).ClosePeriodAsync(customer.Id, season.Id, [1], "ketoan");

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task MoneyPaidBeforeClosingBecomesPartOfThatPeriod()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA", Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 1_000));
        await new PaymentService(t.Db).AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 4), 300, PaymentMethod.Cash, null, "ketoan");
        var service = new SettlementService(t.Db);

        await service.ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "1"), "ketoan");
        var result = await service.GetAsync(customer.Id, season);

        Assert.Equal(300, result!.Periods[0].Paid);
        Assert.Equal(700, result.Periods[0].Remaining);
        Assert.Equal(0, result.Open.Paid);
    }

    [Fact]
    public async Task PaymentCanBeEnteredForAnOlderPeriod_AndOnlyForTheSameSellerAndSeason()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        var other = t.AddCustomer("Nguyễn Văn An");
        await Import(t, "DAKHA", Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 1_000));
        var service = new SettlementService(t.Db);
        var period = (await service.ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "1"), "ketoan")).Value!;
        var payments = new PaymentService(t.Db);

        var ok = await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 8), 400, PaymentMethod.Transfer, null, "ketoan", "Dung", "Vietcombank", period.Id);
        var wrong = await payments.AddAsync(other.Id, season.Id, new DateOnly(2026, 10, 8), 400, PaymentMethod.Cash, null, "ketoan", periodId: period.Id);

        Assert.True(ok.Ok, ok.Error);
        Assert.False(wrong.Ok);
        Assert.Equal(600, (await service.GetAsync(customer.Id, season))!.Periods[0].Remaining);
    }

    [Fact]
    public async Task UndoClose_IsBlockedWhileMoneyWasPaid_AndThenReturnsTheTrucksToWaiting()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA", Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 1_000));
        var service = new SettlementService(t.Db);
        var period = (await service.ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "1"), "ketoan")).Value!;
        var payment = (await new PaymentService(t.Db).AddAsync(
            customer.Id, season.Id, new DateOnly(2026, 10, 8), 400, PaymentMethod.Cash, null, "ketoan", periodId: period.Id)).Value!;

        Assert.False((await service.UndoLastCloseAsync(customer.Id, season.Id)).Ok);

        await new PaymentService(t.Db).VoidAsync(payment.Id, "nhầm", "ketoan");
        Assert.True((await service.UndoLastCloseAsync(customer.Id, season.Id)).Ok);
        Assert.Empty(t.Db.Periods);
        Assert.Null((await t.Db.Payments.SingleAsync()).PeriodId);
        Assert.Null((await t.Db.Tickets.SingleAsync()).PeriodId);
        Assert.Equal(1, (await service.GetAsync(customer.Id, season))!.Open.TruckCount);
    }

    [Fact]
    public async Task BatchClose_GathersEverySellersWaitingTrucksUpToTheDate_AndSkipsThoseWithNone()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var thi = t.AddCustomer("Thi Gia Chiêm");
        var an = t.AddCustomer("Nguyễn Văn An");
        t.AddCustomer("Không Có Xe");
        await Import(t, "DAKHA",
            Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 100),
            Make.Ticket("2", "2026-10-09", "Thi Gia Chiêm", amount: 200),
            Make.Ticket("3", "2026-10-09", "Nguyễn Văn An", amount: 300),
            Make.Ticket("4", "2026-10-20", "Nguyễn Văn An", amount: 400));
        var service = new SettlementService(t.Db);
        await service.ClosePeriodAsync(thi.Id, season.Id, await Ids(t, "1"), "ketoan");

        var preview = (await service.PreviewBatchAsync(season, new DateOnly(2026, 10, 10))).Value!;
        var closed = await service.CloseBatchAsync(season.Id, new DateOnly(2026, 10, 10), "ketoan");

        Assert.Equal(["Nguyễn Văn An", "Thi Gia Chiêm"], preview.Select(r => r.CustomerName));
        var thiRow = preview.Single(r => r.CustomerName == "Thi Gia Chiêm");
        Assert.Equal(1, thiRow.TruckCount);
        Assert.Equal(100, thiRow.CarryIn);
        Assert.Equal(300, thiRow.Payable);
        Assert.Equal(2, closed.Value);
        Assert.Equal(1, await t.Db.Periods.CountAsync(p => p.CustomerId == an.Id));
        Assert.Equal(2, await t.Db.Periods.CountAsync(p => p.CustomerId == thi.Id));
        Assert.Null((await t.Db.Tickets.SingleAsync(x => x.Code == "4")).PeriodId);
    }

    [Fact]
    public async Task Preview_DoesNotSaveAnything_AndShowsTheChosenTotal()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA", Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 1_000), Make.Ticket("2", "2026-10-03", "Thi Gia Chiêm", amount: 500));

        var preview = await new SettlementService(t.Db).PreviewCloseAsync(customer.Id, season, await Ids(t, "1"));

        Assert.True(preview.Ok);
        Assert.Equal(1_000, preview.Value!.Payable);
        Assert.Equal(1, preview.Value.TruckCount);
        Assert.Empty(t.Db.Periods);
        Assert.All(t.Db.Tickets, x => Assert.Null(x.PeriodId));
    }

    [Fact]
    public async Task TruckWeighedTodayButFinishedTomorrow_IsShownThenUpdatedWithoutDuplicating()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        var first = Make.Ticket("2610-0001", "2026-10-03", "Thi Gia Chiêm", amount: null, kg: null) with
        {
            AwaitingSecondWeigh = true, FirstWeightKg = 21_070m
        };
        await Import(t, "DAKHA", first);
        var service = new SettlementService(t.Db);

        var waiting = (await service.GetAsync(customer.Id, season))!.Open;
        Assert.Equal(1, waiting.AwaitingCount);
        Assert.Equal(0, waiting.TicketsTotal);

        await Import(t, "DAKHA", Make.Ticket("2610-0001", "2026-10-03", "Thi Gia Chiêm", amount: null, kg: 16_070m, price: null));
        var unpriced = (await service.GetAsync(customer.Id, season))!.Open;
        Assert.Equal(1, unpriced.NoPriceCount);
        Assert.Equal(0, unpriced.AwaitingCount);

        var priced = Make.Ticket("2610-0001", "2026-10-03", "Thi Gia Chiêm", amount: 317_031_200, kg: 16_022m, price: 19_800m);
        var again = await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", priced), "g.json", "u");

        Assert.Equal(1, again.Value!.Updated);
        Assert.Equal(1, await t.Db.Tickets.CountAsync());
        var done = (await service.GetAsync(customer.Id, season))!.Open;
        Assert.Equal(1, done.PricedCount);
        Assert.Equal(317_031_200, done.TicketsTotal);
    }

    [Fact]
    public async Task PriceArrivingAfterClosing_FlowsIntoTheTrucksPeriodAndIsFlagged()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA",
            Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 1_000),
            Make.Ticket("2", "2026-10-04", "Thi Gia Chiêm", amount: null, price: null));
        var service = new SettlementService(t.Db);
        await service.ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "1", "2"), "ketoan");
        await Task.Delay(20);

        await Import(t, "DAKHA", Make.Ticket("2", "2026-10-04", "Thi Gia Chiêm", amount: 500, price: 500m));

        var period = (await service.GetAsync(customer.Id, season))!.Periods[0];
        Assert.Equal(1_500, period.TicketsTotal);
        Assert.Equal(1, period.ChangedAfterCloseCount);
        Assert.Equal(500, period.ChangedAfterCloseAmount);
        Assert.Equal(0, period.NoPriceCount);
    }

    [Fact]
    public async Task ANewTruckForAnOldDay_WaitsInThePoolInsteadOfJoiningAClosedPeriod()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA", Make.Ticket("1", "2026-10-05", "Thi Gia Chiêm", amount: 1_000));
        var service = new SettlementService(t.Db);
        await service.ClosePeriodAsync(customer.Id, season.Id, await Ids(t, "1"), "ketoan");

        await Import(t, "DAKTO", Make.Ticket("1", "2026-10-05", "Thi Gia Chiêm", amount: 250));
        var settlement = (await service.GetAsync(customer.Id, season))!;

        Assert.Equal(1_000, settlement.Periods[0].TicketsTotal);
        Assert.Equal(1, settlement.Open.TruckCount);
        Assert.Equal("DAKTO", settlement.Open.Tickets[0].StationCode);
    }

    [Fact]
    public async Task SameTicketNumberAtTwoStations_StaysTwoTrucksWithSeparateSubtotals()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA", Make.Ticket("2610-0001", "2026-10-03", "Thi Gia Chiêm", amount: 100));
        await Import(t, "DAKTO", Make.Ticket("2610-0001", "2026-10-03", "Thi Gia Chiêm", amount: 250));

        var open = (await new SettlementService(t.Db).GetAsync(customer.Id, season))!.Open;

        Assert.Equal(2, open.TruckCount);
        Assert.Equal(["Đắk Hà", "Đắk Tô"], open.ByStation.Select(s => s.StationName));
        Assert.Equal(350, open.TicketsTotal);
    }
}
