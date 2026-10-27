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
        DateTimeOffset? updatedAt = null) =>
        new()
        {
            StationCode = station,
            Code = code,
            DisplayNumber = code,
            TicketDate = DateOnly.Parse(date),
            TicketDateTime = DateTimeOffset.Parse($"{date}T08:00:00+07:00"),
            BillableWeightKg = awaiting || amount is null ? null : kg,
            NetWeightKg = awaiting ? null : kg,
            AmountVnd = awaiting ? null : amount,
            AwaitingSecondWeigh = awaiting,
            ContentHash = "x",
            UpdatedAt = updatedAt ?? DateTimeOffset.Parse($"{date}T09:00:00+07:00")
        };

    private static SettlementPeriod Period(int id, string from, string to, string closedAt) =>
        new()
        {
            Id = id, CustomerId = 1, SeasonId = 1, FromDate = DateOnly.Parse(from), ToDate = DateOnly.Parse(to),
            ClosedAt = DateTimeOffset.Parse($"{closedAt}T18:00:00+07:00"), ClosedBy = "ketoan"
        };

    private static Payment Pay(string date, long amount, int? periodId) =>
        new() { Id = (int)amount % 1000, CustomerId = 1, SeasonId = 1, PeriodId = periodId, PaidOn = DateOnly.Parse(date), AmountVnd = amount, CreatedBy = "ketoan" };

    [Fact]
    public void RemainderOfAPartlyPaidPeriodCarriesIntoTheNext_LikeTheOwnersSheet()
    {
        var tickets = new[]
        {
            Ticket("a", "2025-11-03", 434_233_380), Ticket("b", "2025-11-04", 441_184_464),
            Ticket("c", "2025-11-05", 286_418_160), Ticket("d", "2025-11-05", 405_005_328),
            Ticket("e", "2025-11-05", 433_144_656),
            Ticket("f", "2025-11-24", 814_469_240), Ticket("g", "2025-11-25", 784_658_940)
        };
        var periods = new[] { Period(1, "2025-11-01", "2025-11-05", "2025-11-06") };
        var payments = new[] { Pay("2025-11-08", 500_000_000, 1), Pay("2025-11-12", 600_000_000, 1) };

        var result = PeriodBuilder.Build(Season, tickets, periods, payments, Stations);

        var first = result.Periods[0];
        Assert.Equal(1_999_985_988, first.TicketsTotal);
        Assert.Equal(0, first.CarryIn);
        Assert.Equal(1_100_000_000, first.Paid);
        Assert.Equal(899_985_988, first.Remaining);
        Assert.Equal(PeriodStatus.Owing, first.Status);

        var open = result.Open;
        Assert.True(open.IsOpen);
        Assert.Equal(899_985_988, open.CarryIn);
        Assert.Equal(1_599_128_180, open.TicketsTotal);
        Assert.Equal(2_499_114_168, open.Payable);
        Assert.Equal(result.Balance, open.Remaining);
    }

    [Fact]
    public void PayingAPeriodInFullSettlesIt_AndNothingCarries()
    {
        var periods = new[] { Period(1, "2025-11-01", "2025-11-05", "2025-11-06") };

        var result = PeriodBuilder.Build(
            Season, [Ticket("a", "2025-11-03", 1_000)], periods, [Pay("2025-11-08", 1_000, 1)], Stations);

        Assert.Equal(PeriodStatus.Settled, result.Periods[0].Status);
        Assert.Equal(0, result.Open.CarryIn);
    }

    [Fact]
    public void PayingMoreThanThePeriod_IsPaidAheadAndReducesTheNextPeriod()
    {
        var periods = new[] { Period(1, "2025-11-01", "2025-11-05", "2025-11-06") };
        var tickets = new[] { Ticket("a", "2025-11-03", 1_000), Ticket("b", "2025-11-10", 400) };

        var result = PeriodBuilder.Build(Season, tickets, periods, [Pay("2025-11-08", 1_300, 1)], Stations);

        Assert.Equal(PeriodStatus.PaidAhead, result.Periods[0].Status);
        Assert.Equal(-300, result.Periods[0].Remaining);
        Assert.Equal(-300, result.Open.CarryIn);
        Assert.Equal(100, result.Open.Remaining);
    }

    [Fact]
    public void PaymentsWithoutAPeriodCountTowardTheOpenPeriod()
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
        var closed = Period(1, "2025-11-01", "2025-11-05", "2025-11-06");
        var tickets = new[]
        {
            Ticket("a", "2025-11-03", 1_000),
            Ticket("late", "2025-11-04", 700, updatedAt: DateTimeOffset.Parse("2025-11-09T10:00:00+07:00"))
        };

        var view = PeriodBuilder.Build(Season, tickets, [closed], [], Stations).Periods[0];

        Assert.Equal(1, view.ChangedAfterCloseCount);
        Assert.Equal(700, view.ChangedAfterCloseAmount);
        Assert.Equal(1_700, view.TicketsTotal);
    }

    [Fact]
    public void LastRemainderAlwaysEqualsBilledMinusPaid()
    {
        var periods = new[] { Period(1, "2025-11-01", "2025-11-05", "2025-11-06"), Period(2, "2025-11-06", "2025-11-10", "2025-11-11") };
        var tickets = new[] { Ticket("a", "2025-11-03", 900), Ticket("b", "2025-11-08", 800), Ticket("c", "2025-11-20", 700) };
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

    [Fact]
    public async Task ClosingFixesTheDateRange_AndTheNextPeriodStartsTheDayAfter()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA",
            Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 100), Make.Ticket("2", "2026-10-09", "Thi Gia Chiêm", amount: 200));
        var service = new SettlementService(t.Db);

        var first = await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan");
        var second = await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 10), "ketoan");

        Assert.True(first.Ok, first.Error);
        Assert.Equal(new DateOnly(2026, 10, 1), first.Value!.FromDate);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(new DateOnly(2026, 10, 6), second.Value!.FromDate);
    }

    [Fact]
    public async Task ClosingIsRefusedForBadDates()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA", Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 100));
        var service = new SettlementService(t.Db);
        await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan");

        Assert.False((await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan")).Ok);
        Assert.False((await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 8), "ketoan")).Ok);
        Assert.False((await service.ClosePeriodAsync(customer.Id, season.Id, DateOnly.FromDateTime(DateTime.Today).AddDays(1), "ketoan")).Ok);
    }

    [Fact]
    public async Task ClosedSeasonRefusesNewPeriods()
    {
        using var t = new TestDb();
        var season = t.AddSeason(end: "2026-12-31", closed: true);
        var customer = t.AddCustomer("Thi Gia Chiêm");

        var result = await new SettlementService(t.Db).ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan");

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

        await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan");
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
        var period = (await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan")).Value!;
        var payments = new PaymentService(t.Db);

        var ok = await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 8), 400, PaymentMethod.Transfer, null, "ketoan", "Dung", "Vietcombank", period.Id);
        var wrong = await payments.AddAsync(other.Id, season.Id, new DateOnly(2026, 10, 8), 400, PaymentMethod.Cash, null, "ketoan", periodId: period.Id);

        Assert.True(ok.Ok, ok.Error);
        Assert.False(wrong.Ok);
        Assert.Equal(600, (await service.GetAsync(customer.Id, season))!.Periods[0].Remaining);
    }

    [Fact]
    public async Task UndoClose_IsBlockedWhileMoneyWasPaidAgainstThePeriod()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA", Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 1_000));
        var service = new SettlementService(t.Db);
        var period = (await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan")).Value!;
        var payment = (await new PaymentService(t.Db).AddAsync(
            customer.Id, season.Id, new DateOnly(2026, 10, 8), 400, PaymentMethod.Cash, null, "ketoan", periodId: period.Id)).Value!;

        Assert.False((await service.UndoLastCloseAsync(customer.Id, season.Id)).Ok);

        await new PaymentService(t.Db).VoidAsync(payment.Id, "nhầm", "ketoan");
        Assert.True((await service.UndoLastCloseAsync(customer.Id, season.Id)).Ok);
        Assert.Empty(t.Db.Periods);
        Assert.Null((await t.Db.Payments.SingleAsync()).PeriodId);
    }

    [Fact]
    public async Task BatchClose_ClosesEverySellerFromTheirOwnStartDate_AndSkipsThoseWithNoTrucks()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var thi = t.AddCustomer("Thi Gia Chiêm");
        var an = t.AddCustomer("Nguyễn Văn An");
        t.AddCustomer("Không Có Xe");
        await Import(t, "DAKHA",
            Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 100),
            Make.Ticket("2", "2026-10-09", "Thi Gia Chiêm", amount: 200),
            Make.Ticket("3", "2026-10-09", "Nguyễn Văn An", amount: 300));
        var service = new SettlementService(t.Db);
        await service.ClosePeriodAsync(thi.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan");

        var preview = (await service.PreviewBatchAsync(season, new DateOnly(2026, 10, 10))).Value!;
        var closed = await service.CloseBatchAsync(season.Id, new DateOnly(2026, 10, 10), "ketoan");

        Assert.Equal(["Nguyễn Văn An", "Thi Gia Chiêm"], preview.Select(r => r.CustomerName));
        Assert.Equal(new DateOnly(2026, 10, 6), preview.Single(r => r.CustomerName == "Thi Gia Chiêm").From);
        Assert.Equal(100, preview.Single(r => r.CustomerName == "Thi Gia Chiêm").CarryIn);
        Assert.Equal(300, preview.Single(r => r.CustomerName == "Thi Gia Chiêm").Payable);
        Assert.Equal(2, closed.Value);
        Assert.Equal(1, await t.Db.Periods.CountAsync(p => p.CustomerId == an.Id));
        Assert.Equal(2, await t.Db.Periods.CountAsync(p => p.CustomerId == thi.Id));
    }

    [Fact]
    public async Task Preview_DoesNotSaveAnything()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA", Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 1_000));

        var preview = await new SettlementService(t.Db).PreviewCloseAsync(customer.Id, season, new DateOnly(2026, 10, 5));

        Assert.True(preview.Ok);
        Assert.Equal(1_000, preview.Value!.Payable);
        Assert.Empty(t.Db.Periods);
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

        var weighed = Make.Ticket("2610-0001", "2026-10-03", "Thi Gia Chiêm", amount: null, kg: 16_070m, price: null);
        await Import(t, "DAKHA", weighed);
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
    public async Task PriceArrivingAfterClosing_FlowsIntoTheClosedPeriodAndIsFlagged()
    {
        using var t = new TestDb();
        var (season, customer) = Setup(t);
        await Import(t, "DAKHA",
            Make.Ticket("1", "2026-10-03", "Thi Gia Chiêm", amount: 1_000),
            Make.Ticket("2", "2026-10-04", "Thi Gia Chiêm", amount: null, price: null));
        var service = new SettlementService(t.Db);
        await service.ClosePeriodAsync(customer.Id, season.Id, new DateOnly(2026, 10, 5), "ketoan");
        await Task.Delay(20);

        await Import(t, "DAKHA", Make.Ticket("2", "2026-10-04", "Thi Gia Chiêm", amount: 500, price: 500m));

        var period = (await service.GetAsync(customer.Id, season))!.Periods[0];
        Assert.Equal(1_500, period.TicketsTotal);
        Assert.Equal(1, period.ChangedAfterCloseCount);
        Assert.Equal(500, period.ChangedAfterCloseAmount);
        Assert.Equal(0, period.NoPriceCount);
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
