using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

public sealed record CustomerBalance(
    int CustomerId,
    string Name,
    int TicketCount,
    int PendingCount,
    long Billed,
    long Paid,
    long Balance,
    DateOnly? OldestUnpaidDate = null);

public sealed record SeasonOverview(
    Season Season,
    IReadOnlyList<CustomerBalance> Customers,
    long TotalBilled,
    long TotalPaid,
    long TotalBalance,
    int UnmappedTickets,
    int PendingTickets,
    int ExcludedTickets);

public sealed record TicketRow(TicketRecord Ticket, TicketSettlement Settlement);

public sealed record CustomerLedgerView(
    Customer Customer,
    Season Season,
    LedgerResult Ledger,
    IReadOnlyList<TicketRow> Tickets,
    IReadOnlyList<Payment> Payments,
    long CashPaid,
    long TransferPaid,
    IReadOnlyList<StatementLine> Statement)
{
    public decimal TotalBillableKg => Tickets.Where(t => t.Settlement.Status != SettlementStatus.Pending).Sum(t => t.Ticket.BillableWeightKg ?? 0);

    public decimal TotalNetKg => Tickets.Sum(t => t.Ticket.NetWeightKg ?? 0);
}

public sealed class LedgerService(DebtDbContext db)
{
    public async Task<SeasonOverview> GetOverviewAsync(Season season, CancellationToken ct = default)
    {
        var policy = await new SettingsService(db).GetBilledCargoPolicyAsync(ct);
        var tickets = await LoadSeasonTickets(season).ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.SeasonId == season.Id && p.VoidedAt == null)
            .ToListAsync(ct);
        var names = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var billable = tickets.Where(t => policy.IsBilled(t.Cargo)).ToList();
        var mapped = billable.Where(t => t.CustomerId is not null).ToLookup(t => t.CustomerId!.Value);
        var paidBy = payments.ToLookup(p => p.CustomerId);

        var customerIds = mapped.Select(g => g.Key).Union(paidBy.Select(g => g.Key)).Distinct();
        var rows = new List<CustomerBalance>();
        foreach (var id in customerIds)
        {
            var ledger = LedgerCalculator.Compute(
                mapped[id].Select(t => new LedgerTicket(t.Id, t.TicketDateTime, t.AmountVnd)),
                paidBy[id].Sum(p => p.AmountVnd));
            rows.Add(new CustomerBalance(
                id,
                names.GetValueOrDefault(id, "(đã xóa)"),
                mapped[id].Count(),
                mapped[id].Count(t => t.AmountVnd is null),
                ledger.Billed, ledger.Paid, ledger.Balance,
                PeriodBuilder.OldestUnpaid(mapped[id].ToList(), paidBy[id].Sum(p => p.AmountVnd))));
        }

        rows = rows.OrderByDescending(r => r.Balance).ThenBy(r => r.Name).ToList();
        return new SeasonOverview(
            season, rows,
            rows.Sum(r => r.Billed), rows.Sum(r => r.Paid), rows.Sum(r => r.Balance),
            billable.Count(t => t.CustomerId is null),
            billable.Count(t => t.AmountVnd is null),
            tickets.Count - billable.Count);
    }

    public async Task<CustomerLedgerView?> GetCustomerAsync(int customerId, Season season, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null)
            return null;

        var policy = await new SettingsService(db).GetBilledCargoPolicyAsync(ct);
        var tickets = (await LoadSeasonTickets(season).Where(t => t.CustomerId == customerId).ToListAsync(ct))
            .Where(t => policy.IsBilled(t.Cargo))
            .ToList();
        var payments = (await db.Payments.AsNoTracking()
                .Where(p => p.CustomerId == customerId && p.SeasonId == season.Id)
                .ToListAsync(ct))
            .OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id)
            .ToList();
        var live = payments.Where(p => !p.IsVoided).ToList();

        var ledger = LedgerCalculator.Compute(
            tickets.Select(t => new LedgerTicket(t.Id, t.TicketDateTime, t.AmountVnd)),
            live.Sum(p => p.AmountVnd));
        var byId = tickets.ToDictionary(t => t.Id);
        var rows = ledger.Tickets.Select(s => new TicketRow(byId[s.TicketId], s)).ToList();

        var stations = await GetStationNamesAsync(ct);
        return new CustomerLedgerView(
            customer, season, ledger, rows, payments,
            live.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.AmountVnd),
            live.Where(p => p.Method == PaymentMethod.Transfer).Sum(p => p.AmountVnd),
            StatementBuilder.Build(tickets, live, stations));
    }

    /// <summary>Station code → display name, from the newest import of each station.</summary>
    public async Task<Dictionary<string, string>> GetStationNamesAsync(CancellationToken ct = default)
    {
        var batches = await db.ImportBatches.AsNoTracking().Select(b => new { b.StationCode, b.StationName, b.ImportedAt, b.Id }).ToListAsync(ct);
        return batches
            .GroupBy(b => b.StationCode)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.Id).First().StationName);
    }

    private IQueryable<TicketRecord> LoadSeasonTickets(Season season)
    {
        var start = season.StartDate;
        var end = season.EndDate;
        return db.Tickets.AsNoTracking()
            .Where(t => !t.IsDeleted && t.TicketDate >= start && (end == null || t.TicketDate <= end));
    }
}

public sealed record PaymentSuggestions(IReadOnlyList<string> Receivers, IReadOnlyList<string> Payers, IReadOnlyList<string> Banks);

public sealed class PaymentService(DebtDbContext db)
{
    public async Task<OperationResult<Payment>> AddAsync(
        int customerId, int seasonId, DateOnly paidOn, long amountVnd, PaymentMethod method,
        string? note, string user, string? receivedBy = null, string? bankName = null, int? periodId = null,
        CancellationToken ct = default, string? paidBy = null)
    {
        if (amountVnd <= 0)
            return OperationResult<Payment>.Fail("Số tiền trả phải lớn hơn 0.");
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            return OperationResult<Payment>.Fail("Không tìm thấy chủ hàng.");

        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId, ct);
        if (season is null)
            return OperationResult<Payment>.Fail("Không tìm thấy mùa.");
        if (season.IsClosed)
            return OperationResult<Payment>.Fail("Mùa này đã chốt, không trả thêm được.");
        if (!season.Contains(paidOn))
            return OperationResult<Payment>.Fail($"Ngày trả phải từ {season.StartDate:dd/MM/yyyy} trở đi.");

        if (periodId is { } pid && !await db.Periods.AnyAsync(p => p.Id == pid && p.CustomerId == customerId && p.SeasonId == seasonId, ct))
            return OperationResult<Payment>.Fail("Kỳ này không thuộc chủ hàng hoặc mùa đang chọn.");

        var payment = new Payment
        {
            CustomerId = customerId,
            SeasonId = seasonId,
            PeriodId = periodId,
            PaidOn = paidOn,
            AmountVnd = amountVnd,
            Method = method,
            BankName = method != PaymentMethod.Cash && !string.IsNullOrWhiteSpace(bankName) ? bankName.Trim() : null,
            PaidBy = string.IsNullOrWhiteSpace(paidBy) ? null : paidBy.Trim(),
            ReceivedBy = string.IsNullOrWhiteSpace(receivedBy) ? null : receivedBy.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedBy = user,
            CreatedAt = DateTimeOffset.Now
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        return OperationResult<Payment>.Success(payment);
    }

    /// <summary>A payment is never deleted: voiding keeps it on record with who, when and why.</summary>
    public async Task<OperationResult> VoidAsync(int paymentId, string reason, string user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return OperationResult.Fail("Nhập lý do hủy khoản trả.");

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null)
            return OperationResult.Fail("Không tìm thấy khoản trả.");
        if (payment.IsVoided)
            return OperationResult.Fail("Khoản trả này đã hủy rồi.");
        if (await db.Seasons.AnyAsync(s => s.Id == payment.SeasonId && s.IsClosed, ct))
            return OperationResult.Fail("Mùa này đã chốt, không hủy được khoản trả.");

        payment.VoidedAt = DateTimeOffset.Now;
        payment.VoidedBy = user;
        payment.VoidReason = reason.Trim();
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }

    /// <summary>Names typed on earlier payments, most used first, to pick from instead of retyping.</summary>
    public async Task<PaymentSuggestions> GetSuggestionsAsync(CancellationToken ct = default)
    {
        var rows = await db.Payments.AsNoTracking().Where(p => p.VoidedAt == null)
            .Select(p => new { p.ReceivedBy, p.PaidBy, p.BankName }).ToListAsync(ct);
        static List<string> Top(IEnumerable<string?> values) => values
            .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim())
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.Key).Take(20).ToList();
        return new PaymentSuggestions(Top(rows.Select(r => r.ReceivedBy)), Top(rows.Select(r => r.PaidBy)), Top(rows.Select(r => r.BankName)));
    }

    public async Task<List<Payment>> GetRecentAsync(int seasonId, int take = 100, CancellationToken ct = default) =>
        (await db.Payments.AsNoTracking().Include(p => p.Customer)
            .Where(p => p.SeasonId == seasonId).ToListAsync(ct))
        .OrderByDescending(p => p.CreatedAt).Take(take).ToList();
}
