using CanXe.Debt.Data;

namespace CanXe.Debt.Services;

public enum PeriodStatus
{
    /// <summary>The period still being gathered: trucks after the last closed period.</summary>
    Open,
    Owing,
    Settled,

    /// <summary>Paid more than the period came to; the surplus is taken off the next period.</summary>
    PaidAhead
}

/// <summary>Where the cassava of a period came from: one line per scale station.</summary>
public sealed record StationSubtotal(string StationCode, string StationName, int TruckCount, decimal BillableKg, long Amount);

/// <summary>One day at one station, like a line of the hand-kept sheet ("3 xe ngày 24/11 Đắk Hà", with its price).</summary>
public sealed record DayLine(
    DateOnly Date, string StationCode, string StationName, int TruckCount, decimal BillableKg,
    string PricesText, long Amount, int UnfinishedCount);

/// <param name="Number">1-based order the periods were closed in; 0 for the open pool.</param>
public sealed record PeriodView(
    SettlementPeriod? Period,
    int Number,
    DateOnly From,
    DateOnly? To,
    IReadOnlyList<TicketRecord> Tickets,
    IReadOnlyList<StationSubtotal> ByStation,
    int PricedCount,
    int NoPriceCount,
    int AwaitingCount,
    decimal NetKg,
    decimal BillableKg,
    long TicketsTotal,
    long CarryIn,
    long Paid,
    IReadOnlyList<Payment> Payments,
    int ChangedAfterCloseCount,
    long ChangedAfterCloseAmount,
    IReadOnlyList<DayLine> Days)
{
    public bool IsOpen => Period is null;

    public int Id => Period?.Id ?? 0;

    /// <summary>Debt carried in from earlier periods plus this period's priced trucks.</summary>
    public long Payable => CarryIn + TicketsTotal;

    /// <summary>What is still owed after this period's payments; this is what carries into the next period.</summary>
    public long Remaining => Payable - Paid;

    public int TruckCount => Tickets.Count;

    /// <summary>First and last truck by weighing time: the "from ticket … to ticket …" of the period.</summary>
    public TicketRecord? FirstTicket => Tickets.Count == 0 ? null : Tickets[0];

    public TicketRecord? LastTicket => Tickets.Count == 0 ? null : Tickets[^1];

    public bool WaitingForWeightOrPrice => NoPriceCount + AwaitingCount > 0;

    public PeriodStatus Status =>
        IsOpen ? PeriodStatus.Open
        : Remaining > 0 ? PeriodStatus.Owing
        : Remaining < 0 ? PeriodStatus.PaidAhead
        : PeriodStatus.Settled;
}

/// <param name="OldestUnpaidDate">Date of the oldest truck not yet covered by payments (payments counted oldest first); null when nothing is owed.</param>
public sealed record CustomerSettlement(IReadOnlyList<PeriodView> Periods, long TotalBilled, long TotalPaid, DateOnly? OldestUnpaidDate = null)
{
    /// <summary>The still-open period; always the last one.</summary>
    public PeriodView Open => Periods[^1];

    public IEnumerable<PeriodView> Closed => Periods.Where(p => !p.IsOpen);

    public long Balance => TotalBilled - TotalPaid;
}

public static class PeriodBuilder
{
    /// <summary>
    /// Lays one seller's trucks and payments out by settlement period. A truck belongs to the period it was gathered into
    /// (none = the open pool still waiting to be settled), and a payment to the period it was entered for (none = paid ahead,
    /// counted with the open pool). Periods run in the order they were closed; each one's unpaid remainder is the next one's
    /// carried-in debt, so the last remainder always equals total billed minus total paid.
    /// </summary>
    /// <param name="ticketPeriodOf">Overrides which period a truck is in, to preview a period that is not saved yet.</param>
    public static CustomerSettlement Build(
        Season season,
        IEnumerable<TicketRecord> tickets,
        IEnumerable<SettlementPeriod> periods,
        IEnumerable<Payment> payments,
        IReadOnlyDictionary<string, string> stationNames,
        Func<Payment, int?>? periodOf = null,
        Func<TicketRecord, int?>? ticketPeriodOf = null)
    {
        periodOf ??= p => p.PeriodId;
        ticketPeriodOf ??= t => t.PeriodId;
        var allTickets = tickets.Where(t => !t.IsDeleted).ToList();
        var ordered = periods.OrderBy(p => p.Id <= 0 ? int.MaxValue : p.Id).ThenBy(p => p.ClosedAt).ToList();
        var livePayments = payments.Where(p => !p.IsVoided).ToList();

        var views = new List<PeriodView>();
        long carry = 0;
        foreach (var (period, index) in ordered.Select((p, i) => (p, i)))
        {
            var view = BuildView(
                period, index + 1,
                allTickets.Where(t => ticketPeriodOf(t) == period.Id).ToList(),
                livePayments.Where(p => periodOf(p) == period.Id).ToList(),
                carry, stationNames, season.StartDate);
            views.Add(view);
            carry = view.Remaining;
        }

        views.Add(BuildView(
            null, 0,
            allTickets.Where(t => ticketPeriodOf(t) is null).ToList(),
            livePayments.Where(p => periodOf(p) is null).ToList(),
            carry, stationNames, season.StartDate));

        return new CustomerSettlement(
            views,
            views.Sum(v => v.TicketsTotal),
            views.Sum(v => v.Paid),
            OldestUnpaid(allTickets, livePayments.Sum(p => p.AmountVnd)));
    }

    /// <summary>
    /// How far back the debt reaches: lump payments settle the oldest trucks first, and the first truck still not fully
    /// covered marks the start of the debt.
    /// </summary>
    public static DateOnly? OldestUnpaid(IReadOnlyList<TicketRecord> tickets, long totalPaid)
    {
        var ledger = LedgerCalculator.Compute(
            tickets.Select((t, i) => new LedgerTicket(i, t.TicketDateTime, t.Stage == TicketStage.Priced ? t.AmountVnd : null)),
            totalPaid);
        var first = ledger.Tickets.FirstOrDefault(s => s.Status is SettlementStatus.Unpaid or SettlementStatus.Partial);
        return first is null ? null : tickets[first.TicketId].TicketDate;
    }

    private static string PriceText(IEnumerable<TicketRecord> priced) =>
        string.Join(" · ", priced
            .Select(t => t.HasPriceSplit && t.SecondUnitPrice is { } second ? $"{Money(t.UnitPrice)}/{Money(second)}" : Money(t.UnitPrice))
            .Distinct());

    private static string Money(decimal? value) => value is { } v ? v.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.') : "—";

    private static PeriodView BuildView(
        SettlementPeriod? period, int number,
        List<TicketRecord> tickets, List<Payment> payments, long carryIn,
        IReadOnlyDictionary<string, string> stationNames, DateOnly seasonStart)
    {
        var sorted = tickets.OrderBy(t => t.TicketDateTime).ThenBy(t => t.StationCode).ThenBy(t => t.Code).ToList();
        var from = sorted.Count > 0 ? sorted.Min(t => t.TicketDate) : period?.FromDate ?? seasonStart;
        DateOnly? to = period is null ? null : sorted.Count > 0 ? sorted.Max(t => t.TicketDate) : period.ToDate;
        var priced = sorted.Where(t => t.Stage == TicketStage.Priced).ToList();
        var byStation = sorted
            .GroupBy(t => t.StationCode)
            .OrderBy(g => g.Key)
            .Select(g => new StationSubtotal(
                g.Key,
                stationNames.GetValueOrDefault(g.Key, g.Key),
                g.Count(),
                g.Where(t => t.Stage == TicketStage.Priced).Sum(t => t.BillableWeightKg ?? 0),
                g.Where(t => t.Stage == TicketStage.Priced).Sum(t => t.AmountVnd ?? 0)))
            .ToList();

        var days = sorted
            .GroupBy(t => (t.TicketDate, t.StationCode))
            .OrderBy(g => g.Key.TicketDate).ThenBy(g => g.Key.StationCode)
            .Select(g =>
            {
                var pricedInDay = g.Where(t => t.Stage == TicketStage.Priced).ToList();
                return new DayLine(
                    g.Key.TicketDate, g.Key.StationCode, stationNames.GetValueOrDefault(g.Key.StationCode, g.Key.StationCode),
                    g.Count(), pricedInDay.Sum(t => t.BillableWeightKg ?? 0), PriceText(pricedInDay),
                    pricedInDay.Sum(t => t.AmountVnd ?? 0), g.Count(t => t.Stage != TicketStage.Priced));
            })
            .ToList();

        var changed = period is null
            ? []
            : sorted.Where(t => t.UpdatedAt > period.ClosedAt).ToList();

        return new PeriodView(
            period, number, from, to, sorted, byStation,
            priced.Count,
            sorted.Count(t => t.Stage == TicketStage.NoPrice),
            sorted.Count(t => t.Stage == TicketStage.AwaitingSecondWeigh),
            sorted.Where(t => t.Stage != TicketStage.AwaitingSecondWeigh).Sum(t => t.NetWeightKg ?? 0),
            priced.Sum(t => t.BillableWeightKg ?? 0),
            priced.Sum(t => t.AmountVnd ?? 0),
            carryIn,
            payments.Sum(p => p.AmountVnd),
            payments.OrderBy(p => p.PaidOn).ThenBy(p => p.Id).ToList(),
            changed.Count,
            changed.Sum(t => t.AmountVnd ?? 0),
            days);
    }
}
