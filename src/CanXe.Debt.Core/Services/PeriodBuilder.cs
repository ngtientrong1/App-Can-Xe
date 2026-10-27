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

public sealed record PeriodView(
    SettlementPeriod? Period,
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
    long ChangedAfterCloseAmount)
{
    public bool IsOpen => Period is null;

    public int Id => Period?.Id ?? 0;

    /// <summary>Debt carried in from earlier periods plus this period's priced trucks.</summary>
    public long Payable => CarryIn + TicketsTotal;

    /// <summary>What is still owed after this period's payments; this is what carries into the next period.</summary>
    public long Remaining => Payable - Paid;

    public int TruckCount => Tickets.Count;

    public bool WaitingForWeightOrPrice => NoPriceCount + AwaitingCount > 0;

    public PeriodStatus Status =>
        IsOpen ? PeriodStatus.Open
        : Remaining > 0 ? PeriodStatus.Owing
        : Remaining < 0 ? PeriodStatus.PaidAhead
        : PeriodStatus.Settled;
}

public sealed record CustomerSettlement(IReadOnlyList<PeriodView> Periods, long TotalBilled, long TotalPaid)
{
    /// <summary>The still-open period; always the last one.</summary>
    public PeriodView Open => Periods[^1];

    public IEnumerable<PeriodView> Closed => Periods.Where(p => !p.IsOpen);

    public long Balance => TotalBilled - TotalPaid;
}

public static class PeriodBuilder
{
    /// <summary>
    /// Lays one seller's trucks and payments out by settlement period. Trucks belong to a period by their date, and a
    /// payment to the period it was entered for (none = the open period). Each period's unpaid remainder is the next
    /// period's carried-in debt, so the last remainder always equals total billed minus total paid.
    /// </summary>
    public static CustomerSettlement Build(
        Season season,
        IEnumerable<TicketRecord> tickets,
        IEnumerable<SettlementPeriod> periods,
        IEnumerable<Payment> payments,
        IReadOnlyDictionary<string, string> stationNames,
        Func<Payment, int?>? periodOf = null)
    {
        periodOf ??= p => p.PeriodId;
        var allTickets = tickets.Where(t => !t.IsDeleted).ToList();
        var ordered = periods.OrderBy(p => p.FromDate).ToList();
        var livePayments = payments.Where(p => !p.IsVoided).ToList();

        var views = new List<PeriodView>();
        long carry = 0;
        foreach (var period in ordered)
        {
            var view = BuildView(
                period, period.FromDate, period.ToDate,
                allTickets.Where(t => t.TicketDate >= period.FromDate && t.TicketDate <= period.ToDate).ToList(),
                livePayments.Where(p => periodOf(p) == period.Id).ToList(),
                carry, stationNames);
            views.Add(view);
            carry = view.Remaining;
        }

        var openFrom = ordered.Count == 0 ? season.StartDate : ordered[^1].ToDate.AddDays(1);
        views.Add(BuildView(
            null, openFrom, null,
            allTickets.Where(t => t.TicketDate >= openFrom).ToList(),
            livePayments.Where(p => periodOf(p) is null).ToList(),
            carry, stationNames));

        return new CustomerSettlement(
            views,
            views.Sum(v => v.TicketsTotal),
            views.Sum(v => v.Paid));
    }

    private static PeriodView BuildView(
        SettlementPeriod? period, DateOnly from, DateOnly? to,
        List<TicketRecord> tickets, List<Payment> payments, long carryIn,
        IReadOnlyDictionary<string, string> stationNames)
    {
        var sorted = tickets.OrderBy(t => t.TicketDateTime).ThenBy(t => t.StationCode).ThenBy(t => t.Code).ToList();
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

        var changed = period is null
            ? []
            : sorted.Where(t => t.UpdatedAt > period.ClosedAt).ToList();

        return new PeriodView(
            period, from, to, sorted, byStation,
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
            changed.Sum(t => t.AmountVnd ?? 0));
    }
}
