namespace CanXe.Debt.Services;

public enum SettlementStatus
{
    /// <summary>The ticket has no price yet, so it is not in the debt.</summary>
    Pending,
    Unpaid,
    Partial,
    Paid
}

public sealed record LedgerTicket(int TicketId, DateTimeOffset At, long? Amount);

public sealed record TicketSettlement(int TicketId, long? Amount, long Allocated, SettlementStatus Status)
{
    public long Remaining => Amount is { } amount ? amount - Allocated : 0;
}

/// <param name="Balance">Billed − Paid. Negative means the customer has paid ahead.</param>
public sealed record LedgerResult(
    long Billed,
    long Paid,
    long Balance,
    IReadOnlyList<TicketSettlement> Tickets)
{
    public long Credit => Balance < 0 ? -Balance : 0;
}

public static class LedgerCalculator
{
    /// <summary>
    /// Running-balance ledger: every payment reduces the customer's total debt. Payments are then
    /// allocated to the oldest tickets first (FIFO) only to show which tickets are settled.
    /// </summary>
    public static LedgerResult Compute(IEnumerable<LedgerTicket> tickets, long paid)
    {
        var ordered = tickets.OrderBy(t => t.At).ThenBy(t => t.TicketId).ToList();
        var billed = ordered.Sum(t => t.Amount ?? 0);
        var remainingPaid = Math.Max(paid, 0);
        var rows = new List<TicketSettlement>(ordered.Count);

        foreach (var ticket in ordered)
        {
            if (ticket.Amount is not { } amount)
            {
                rows.Add(new TicketSettlement(ticket.TicketId, null, 0, SettlementStatus.Pending));
                continue;
            }

            var allocated = Math.Min(remainingPaid, amount);
            remainingPaid -= allocated;
            var status = allocated >= amount ? SettlementStatus.Paid
                : allocated > 0 ? SettlementStatus.Partial
                : SettlementStatus.Unpaid;
            rows.Add(new TicketSettlement(ticket.TicketId, amount, allocated, status));
        }

        return new LedgerResult(billed, paid, billed - paid, rows);
    }
}
