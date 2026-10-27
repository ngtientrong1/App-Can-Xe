using CanXe.Debt.Data;

namespace CanXe.Debt.Services;

public enum StatementLineKind
{
    Tickets,
    Payment
}

/// <summary>One line of a customer's debt statement: a batch of trucks (debit) or a payment (credit).</summary>
public sealed record StatementLine(
    StatementLineKind Kind,
    DateOnly Date,
    string Description,
    long Debit,
    long Credit,
    long Balance,
    IReadOnlyList<TicketRecord> Tickets,
    Payment? Payment,
    int PendingCount)
{
    public decimal TotalBillableKg => Tickets.Sum(t => t.BillableWeightKg ?? 0);
}

public static class StatementBuilder
{
    /// <summary>
    /// Chronological statement with a running balance, like the hand-kept sheet: trucks of the same day and station are
    /// one line ("3 xe Đắk Tô"), every payment is its own line, and the balance after each line is "còn nợ".
    /// </summary>
    public static IReadOnlyList<StatementLine> Build(
        IEnumerable<TicketRecord> tickets,
        IEnumerable<Payment> payments,
        IReadOnlyDictionary<string, string> stationNames)
    {
        var entries = new List<(DateOnly Date, int Order, DateTimeOffset At, Func<long, StatementLine> Make, long Delta)>();

        foreach (var group in tickets.GroupBy(t => (t.TicketDate, t.StationCode)))
        {
            var rows = group.OrderBy(t => t.TicketDateTime).ThenBy(t => t.Code).ToList();
            var debit = rows.Sum(t => t.AmountVnd ?? 0);
            var pending = rows.Count(t => t.AmountVnd is null);
            var station = stationNames.GetValueOrDefault(group.Key.StationCode, group.Key.StationCode);
            var description = Describe(rows, station);
            var date = group.Key.TicketDate;
            entries.Add((date, 0, rows[0].TicketDateTime,
                balance => new StatementLine(StatementLineKind.Tickets, date, description, debit, 0, balance, rows, null, pending),
                debit));
        }

        foreach (var payment in payments.Where(p => !p.IsVoided))
        {
            var description = Describe(payment);
            entries.Add((payment.PaidOn, 1, payment.CreatedAt,
                balance => new StatementLine(StatementLineKind.Payment, payment.PaidOn, description, 0, payment.AmountVnd, balance, [], payment, 0),
                -payment.AmountVnd));
        }

        var lines = new List<StatementLine>(entries.Count);
        long running = 0;
        foreach (var entry in entries.OrderBy(e => e.Date).ThenBy(e => e.Order).ThenBy(e => e.At))
        {
            running += entry.Delta;
            lines.Add(entry.Make(running));
        }

        return lines;
    }

    private static string Describe(IReadOnlyList<TicketRecord> rows, string station)
    {
        var numbers = rows.Count == 1
            ? rows[0].DisplayNumber
            : $"{rows[0].DisplayNumber} – {rows[^1].DisplayNumber}";
        return $"{rows.Count} xe · {station} · phiếu {numbers}";
    }

    private static string Describe(Payment payment)
    {
        var parts = new List<string>
        {
            payment.Method == PaymentMethod.Cash
                ? "Trả tiền mặt"
                : string.IsNullOrWhiteSpace(payment.BankName) ? "Trả chuyển khoản" : $"Trả chuyển khoản ({payment.BankName})"
        };
        if (!string.IsNullOrWhiteSpace(payment.ReceivedBy))
            parts.Add($"{payment.ReceivedBy} nhận");
        if (!string.IsNullOrWhiteSpace(payment.Note))
            parts.Add(payment.Note!);
        return string.Join(" · ", parts);
    }
}
