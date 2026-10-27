using CanXe.Debt.Data;
using CanXe.Debt.Services;

namespace CanXe.Debt.Tests;

public sealed class StatementBuilderTests
{
    private static readonly Dictionary<string, string> Stations = new() { ["DAKHA"] = "Đắk Hà", ["DAKTO"] = "Đắk Tô" };

    private static TicketRecord Ticket(string station, string code, string number, string date, long? amount, decimal kg = 1000m) =>
        new()
        {
            StationCode = station,
            Code = code,
            DisplayNumber = number,
            TicketDate = DateOnly.Parse(date),
            TicketDateTime = DateTimeOffset.Parse($"{date}T08:00:00+07:00"),
            BillableWeightKg = kg,
            AmountVnd = amount,
            ContentHash = "x"
        };

    private static Payment Payment(string date, long amount, PaymentMethod method = PaymentMethod.Cash, string? receivedBy = null, string? bank = null, string? note = null) =>
        new()
        {
            CustomerId = 1,
            SeasonId = 1,
            PaidOn = DateOnly.Parse(date),
            AmountVnd = amount,
            Method = method,
            ReceivedBy = receivedBy,
            BankName = bank,
            Note = note,
            CreatedBy = "ketoan",
            CreatedAt = DateTimeOffset.Parse($"{date}T18:00:00+07:00")
        };

    [Fact]
    public void ReproducesTheOwnersHandKeptSheet()
    {
        // 3–5/11: five trucks, then payments of 500, 600 and 900 million.
        var tickets = new[]
        {
            Ticket("DAKHA", "15", "15/11", "2025-11-03", 434_233_380),
            Ticket("DAKHA", "30", "30/11", "2025-11-04", 441_184_464),
            Ticket("DAKHA", "31", "31/11", "2025-11-05", 286_418_160),
            Ticket("DAKHA", "37", "37/11", "2025-11-05", 405_005_328),
            Ticket("DAKHA", "40", "40/11", "2025-11-05", 433_144_656)
        };
        var payments = new[]
        {
            Payment("2025-11-08", 500_000_000, receivedBy: "Dung", note: "nhận dùm"),
            Payment("2025-11-12", 600_000_000, receivedBy: "A Thắng"),
            Payment("2025-11-20", 900_000_000, receivedBy: "A Thắng")
        };

        var lines = StatementBuilder.Build(tickets, payments, Stations);

        Assert.Equal(
            [434_233_380L, 875_417_844L, 1_999_985_988L, 1_499_985_988L, 899_985_988L, -14_012L],
            lines.Select(l => l.Balance));
        Assert.Equal(1_999_985_988, lines.Where(l => l.Kind == StatementLineKind.Tickets).Sum(l => l.Debit));
    }

    [Fact]
    public void TrucksOfTheSameDayAndStationShareALine_ButStationsStaySeparate()
    {
        var tickets = new[]
        {
            Ticket("DAKHA", "a", "218/11", "2025-11-24", 100),
            Ticket("DAKHA", "b", "219/11", "2025-11-24", 200),
            Ticket("DAKTO", "c", "1/11", "2025-11-24", 300)
        };

        var lines = StatementBuilder.Build(tickets, [], Stations);

        Assert.Equal(2, lines.Count);
        var ha = lines.Single(l => l.Description.Contains("Đắk Hà"));
        Assert.Equal(2, ha.Tickets.Count);
        Assert.Equal(300, ha.Debit);
        Assert.Contains("218/11 – 219/11", ha.Description);
        Assert.Contains("1 xe · Đắk Tô", lines.Single(l => l.Description.Contains("Đắk Tô")).Description);
        Assert.Equal(600, lines[^1].Balance);
    }

    [Fact]
    public void TrucksComeBeforePaymentsOnTheSameDay()
    {
        var lines = StatementBuilder.Build(
            [Ticket("DAKHA", "a", "1/11", "2025-11-08", 1000)],
            [Payment("2025-11-08", 400)],
            Stations);

        Assert.Equal([StatementLineKind.Tickets, StatementLineKind.Payment], lines.Select(l => l.Kind));
        Assert.Equal([1000L, 600L], lines.Select(l => l.Balance));
    }

    [Fact]
    public void PaymentDescription_NamesWhoCollectedAndWhichBank()
    {
        var lines = StatementBuilder.Build(
            [],
            [
                Payment("2025-11-08", 1, PaymentMethod.Cash, receivedBy: "Dung", note: "nhận dùm"),
                Payment("2025-11-09", 1, PaymentMethod.Transfer, bank: "Vietcombank")
            ],
            Stations);

        Assert.Equal("Trả tiền mặt · Dung nhận · nhận dùm", lines[0].Description);
        Assert.Equal("Trả chuyển khoản (Vietcombank)", lines[1].Description);
    }

    [Fact]
    public void VoidedPaymentsAreLeftOut()
    {
        var voided = Payment("2025-11-08", 500);
        voided.VoidedAt = DateTimeOffset.Now;

        var lines = StatementBuilder.Build([Ticket("DAKHA", "a", "1/11", "2025-11-07", 1000)], [voided], Stations);

        Assert.Single(lines);
        Assert.Equal(1000, lines[0].Balance);
    }

    [Fact]
    public void TrucksWithoutAPrice_AreCountedAsPendingAndAddNothing()
    {
        var lines = StatementBuilder.Build(
            [Ticket("DAKHA", "a", "1/11", "2025-11-07", null), Ticket("DAKHA", "b", "2/11", "2025-11-07", 500)],
            [],
            Stations);

        var line = Assert.Single(lines);
        Assert.Equal(1, line.PendingCount);
        Assert.Equal(500, line.Debit);
    }

    [Fact]
    public void LineReportsTotalBillableKg()
    {
        var lines = StatementBuilder.Build(
            [Ticket("DAKHA", "a", "1/11", "2025-11-07", 1, 17_796), Ticket("DAKHA", "b", "2/11", "2025-11-07", 1, 11_366)],
            [],
            Stations);

        Assert.Equal(29_162, lines[0].TotalBillableKg);
    }
}
