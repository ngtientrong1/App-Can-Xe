using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

/// <summary>Cases taken from the owner's hand-kept sheets (Cà Phê 2025.xlsx).</summary>
public sealed class SheetCaseTests
{
    private static readonly Dictionary<string, string> Stations = new() { ["DAKHA"] = "Đắk Hà", ["DAKTO"] = "Đắk Tô" };
    private static readonly Season Season = new() { Id = 1, Name = "Mùa cà", StartDate = new DateOnly(2025, 11, 1) };

    private static TicketRecord Ticket(string code, string date, long? amount, decimal? price = 25_700m, string station = "DAKHA", decimal kg = 1000m) =>
        new()
        {
            StationCode = station, Code = code, DisplayNumber = code,
            TicketDate = DateOnly.Parse(date), TicketDateTime = DateTimeOffset.Parse($"{date}T08:00:00+07:00"),
            BillableWeightKg = amount is null ? null : kg, NetWeightKg = kg, AmountVnd = amount, UnitPrice = price,
            ContentHash = "x", UpdatedAt = DateTimeOffset.Parse($"{date}T09:00:00+07:00")
        };

    private static Payment Pay(string date, long amount, int? periodId = null) =>
        new() { CustomerId = 1, SeasonId = 1, PeriodId = periodId, PaidOn = DateOnly.Parse(date), AmountVnd = amount, CreatedBy = "ketoan" };

    [Fact]
    public void DayLines_ListEachDayAtEachStationWithItsPrices()
    {
        var tickets = new[]
        {
            Ticket("a", "2025-11-26", 100, 26_700m), Ticket("b", "2025-11-26", 200, 26_700m),
            Ticket("c", "2025-11-26", 300, 26_600m, "DAKTO"),
            Ticket("d", "2025-11-27", 400, 26_700m), Ticket("e", "2025-11-27", null, null)
        };

        var days = PeriodBuilder.Build(Season, tickets, [], [], Stations).Open.Days;

        Assert.Equal(3, days.Count);
        Assert.Equal(("Đắk Hà", 2, 300L, "26.700"), (days[0].StationName, days[0].TruckCount, days[0].Amount, days[0].PricesText));
        Assert.Equal(("Đắk Tô", 1, "26.600"), (days[1].StationName, days[1].TruckCount, days[1].PricesText));
        Assert.Equal((2, 1), (days[2].TruckCount, days[2].UnfinishedCount));
    }

    [Fact]
    public void OldestUnpaid_IsTheFirstTruckTheLumpPaymentsDoNotCover()
    {
        var tickets = new[]
        {
            Ticket("a", "2025-11-01", 1_000), Ticket("b", "2025-11-05", 1_000), Ticket("c", "2025-11-09", 1_000)
        };

        Assert.Equal(new DateOnly(2025, 11, 5), PeriodBuilder.OldestUnpaid(tickets, 1_500));
        Assert.Equal(new DateOnly(2025, 11, 1), PeriodBuilder.OldestUnpaid(tickets, 0));
        Assert.Equal(new DateOnly(2025, 11, 9), PeriodBuilder.OldestUnpaid(tickets, 2_000));
        Assert.Null(PeriodBuilder.OldestUnpaid(tickets, 3_000));
        Assert.Null(PeriodBuilder.OldestUnpaid(tickets, 9_000));
    }

    [Fact]
    public void OldestUnpaid_IgnoresTrucksNotYetPriced()
    {
        var tickets = new[] { Ticket("a", "2025-11-01", null, null), Ticket("b", "2025-11-05", 1_000) };

        Assert.Equal(new DateOnly(2025, 11, 5), PeriodBuilder.OldestUnpaid(tickets, 0));
    }

    [Fact]
    public void SettlementCarriesTheOldestUnpaidDate()
    {
        var tickets = new[] { Ticket("a", "2025-11-03", 1_000), Ticket("b", "2025-11-10", 1_000) };

        var result = PeriodBuilder.Build(Season, tickets, [], [Pay("2025-11-05", 1_200)], Stations);

        Assert.Equal(new DateOnly(2025, 11, 10), result.OldestUnpaidDate);
    }

    [Fact]
    public void AdvancePaidBeforeTheTrucksArrive_ReducesTheTotalLikeInTheSheet()
    {
        // Hoa Xíu: "Tổng nợ 5.650.143.158 − Tổng ứng 2.000.000.000 = 3.650.143.158".
        var tickets = new[] { Ticket("a", "2025-11-21", 5_650_143_158) };

        var result = PeriodBuilder.Build(Season, tickets, [], [Pay("2025-11-24", 1_000_000_000), Pay("2025-11-27", 1_000_000_000)], Stations);

        Assert.Equal(3_650_143_158, result.Open.Remaining);
        Assert.Equal(result.Balance, result.Open.Remaining);
    }

    [Fact]
    public void Statement_NamesWhoPaidAndWhoReceived_AndOffsetsAreNotCash()
    {
        var cash = Pay("2025-11-29", 700_000_000); cash.PaidBy = "Bố"; cash.ReceivedBy = "C Hồng";
        var offset = Pay("2025-11-09", 3_555_000_000); offset.Method = PaymentMethod.Offset; offset.Note = "30 tấn cà nhân chuyển sang";

        var lines = StatementBuilder.Build([], [cash, offset], Stations);

        Assert.Equal("Bù trừ công nợ · 30 tấn cà nhân chuyển sang", lines[0].Description);
        Assert.Equal("Trả tiền mặt · Bố chi · C Hồng nhận", lines[1].Description);
    }
}

public sealed class SheetCaseServiceTests
{
    [Fact]
    public async Task TicketNotesFromTheScale_AreImportedAndUpdated()
    {
        using var t = new TestDb();
        t.AddSeason();
        var import = new ImportService(t.Db);
        await import.ImportAsync(Make.File("DAKHA", Make.Ticket("1") with { Notes = "CÀ GỬI" }), "f", "u");

        Assert.Equal("CÀ GỬI", (await t.Db.Tickets.SingleAsync()).Notes);

        await import.ImportAsync(Make.File("DAKHA", Make.Ticket("1") with { Notes = " Lô Ngọc Hồi " }), "g", "u");

        Assert.Equal("Lô Ngọc Hồi", (await t.Db.Tickets.SingleAsync()).Notes);
    }

    [Fact]
    public async Task OverviewShowsHowLongEachSellerHasBeenOwed()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("Trang");
        await new ImportService(t.Db).ImportAsync(
            Make.File("DAKHA", Make.Ticket("1", "2026-10-03", "Trang", amount: 1_000), Make.Ticket("2", "2026-10-09", "Trang", amount: 1_000)), "f", "u");
        await new PaymentService(t.Db).AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 10), 1_500, PaymentMethod.Cash, null, "u");

        var row = Assert.Single((await new LedgerService(t.Db).GetOverviewAsync(season)).Customers);

        Assert.Equal(new DateOnly(2026, 10, 9), row.OldestUnpaidDate);
    }

    [Fact]
    public async Task PaymentOptions_BankIsKeptForOffsetsToo_AndPayerIsRecorded()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("Trang");
        var payments = new PaymentService(t.Db);

        await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 10), 100, PaymentMethod.Cash, null, "u", "Minh", "Vietcombank", paidBy: "Bố");
        await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 11), 200, PaymentMethod.Offset, "cà nhân", "u", bankName: "Cà nhân");

        var saved = await t.Db.Payments.OrderBy(p => p.Id).ToListAsync();
        Assert.Null(saved[0].BankName);
        Assert.Equal("Bố", saved[0].PaidBy);
        Assert.Equal("Cà nhân", saved[1].BankName);
    }

    [Fact]
    public async Task Suggestions_ListMostUsedNamesFirst()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("Trang");
        var payments = new PaymentService(t.Db);
        foreach (var receiver in new[] { "Minh", "Dung", "Dung", "dung" })
            await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 10), 100, PaymentMethod.Transfer, null, "u", receiver, "VCB");

        var suggestions = await payments.GetSuggestionsAsync();

        Assert.Equal(["Dung", "Minh"], suggestions.Receivers);
        Assert.Equal(["VCB"], suggestions.Banks);
    }
}

public sealed class FreeAmountPaymentTests
{
    private static readonly Dictionary<string, string> Stations = new() { ["DAKHA"] = "Đắk Hà" };
    private static readonly Season Season = new() { Id = 1, Name = "Mùa", StartDate = new DateOnly(2025, 11, 1) };

    private static TicketRecord Ticket(string code, string date, long amount) =>
        new()
        {
            StationCode = "DAKHA", Code = code, DisplayNumber = code, TicketDate = DateOnly.Parse(date),
            TicketDateTime = DateTimeOffset.Parse($"{date}T08:00:00+07:00"), BillableWeightKg = 1000, AmountVnd = amount,
            UnitPrice = 1, ContentHash = "x", UpdatedAt = DateTimeOffset.Now
        };

    [Fact]
    public void AdvanceOfAnyAmount_CoversTheOldestTrucksFirst_AndShowsWhichAreSettled()
    {
        var tickets = new[] { Ticket("a", "2025-11-03", 100), Ticket("b", "2025-11-04", 100), Ticket("c", "2025-11-05", 100) };
        var advance = new Payment { CustomerId = 1, SeasonId = 1, PaidOn = new DateOnly(2025, 11, 6), AmountVnd = 150, CreatedBy = "kt" };

        var open = PeriodBuilder.Build(Season, tickets, [], [advance], Stations).Open;

        Assert.Equal(
            [SettlementStatus.Paid, SettlementStatus.Partial, SettlementStatus.Unpaid],
            open.TruckStatuses.Select(s => s.Status));
        Assert.Equal(50, open.TruckStatuses[1].Allocated);
        Assert.Equal(150, open.Payable - open.Remaining);
    }

    [Fact]
    public void AdvanceGivenBeforeTheTrucksArrive_SettlesThemAsTheyCome()
    {
        var advance = new Payment { CustomerId = 1, SeasonId = 1, PaidOn = new DateOnly(2025, 11, 1), AmountVnd = 100, CreatedBy = "kt" };

        var none = PeriodBuilder.Build(Season, [], [], [advance], Stations).Open;
        var later = PeriodBuilder.Build(Season, [Ticket("a", "2025-11-03", 60), Ticket("b", "2025-11-04", 80)], [], [advance], Stations).Open;

        Assert.Equal(-100, none.Remaining);
        Assert.Equal(40, later.Remaining);
        Assert.Equal([SettlementStatus.Paid, SettlementStatus.Partial], later.TruckStatuses.Select(s => s.Status));
    }

    [Fact]
    public async Task UndoRightAfterEntering_VoidsThePayment_ButOnlyForItsAuthorAndOnlyBrieflyAfter()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("A");
        var payments = new PaymentService(t.Db);
        var mine = (await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 10), 100, PaymentMethod.Cash, null, "ketoan")).Value!;
        var old = (await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 10), 200, PaymentMethod.Cash, null, "ketoan")).Value!;

        Assert.False((await payments.UndoRecentAsync(mine.Id, "admin")).Ok);
        Assert.False((await payments.UndoRecentAsync(old.Id, "ketoan", DateTimeOffset.Now.AddMinutes(16))).Ok);
        Assert.True((await payments.UndoRecentAsync(mine.Id, "KeToan")).Ok);
        Assert.False((await payments.UndoRecentAsync(mine.Id, "ketoan")).Ok);

        var voided = await t.Db.Payments.SingleAsync(p => p.Id == mine.Id);
        Assert.True(voided.IsVoided);
        Assert.Equal("Hoàn tác ngay sau khi nhập", voided.VoidReason);
        Assert.False((await t.Db.Payments.SingleAsync(p => p.Id == old.Id)).IsVoided);
    }
}