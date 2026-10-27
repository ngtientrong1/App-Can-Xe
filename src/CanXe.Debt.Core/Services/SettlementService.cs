using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

/// <summary>One seller's row in the "close periods for everyone" preview.</summary>
public sealed record BatchRow(
    int CustomerId,
    string CustomerName,
    DateOnly From,
    int TruckCount,
    int UnfinishedCount,
    long TicketsTotal,
    long CarryIn,
    long Payable);

public sealed class SettlementService(DebtDbContext db)
{
    public async Task<CustomerSettlement?> GetAsync(int customerId, Season season, CancellationToken ct = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            return null;

        var data = await LoadAsync(customerId, season, ct);
        return PeriodBuilder.Build(season, data.Tickets, data.Periods, data.Payments, data.Stations);
    }

    /// <summary>What closing the open period through <paramref name="toDate"/> would look like, without saving it.</summary>
    public async Task<OperationResult<PeriodView>> PreviewCloseAsync(
        int customerId, Season season, DateOnly toDate, CancellationToken ct = default)
    {
        var data = await LoadAsync(customerId, season, ct);
        var problem = ValidateClose(season, data.Periods, data.Tickets, toDate, out var from);
        if (problem is not null)
            return OperationResult<PeriodView>.Fail(problem);

        const int hypotheticalId = -1;
        var hypothetical = new SettlementPeriod
        {
            Id = hypotheticalId, CustomerId = customerId, SeasonId = season.Id,
            FromDate = from, ToDate = toDate, ClosedAt = DateTimeOffset.Now, ClosedBy = ""
        };
        var settlement = PeriodBuilder.Build(
            season, data.Tickets, data.Periods.Append(hypothetical), data.Payments, data.Stations,
            p => p.PeriodId ?? hypotheticalId);
        return OperationResult<PeriodView>.Success(settlement.Periods.First(p => p.Id == hypotheticalId));
    }

    public async Task<OperationResult<SettlementPeriod>> ClosePeriodAsync(
        int customerId, int seasonId, DateOnly toDate, string user, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId, ct);
        if (season is null)
            return OperationResult<SettlementPeriod>.Fail("Không tìm thấy mùa.");
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            return OperationResult<SettlementPeriod>.Fail("Không tìm thấy chủ hàng.");

        var data = await LoadAsync(customerId, season, ct);
        var problem = ValidateClose(season, data.Periods, data.Tickets, toDate, out var from);
        if (problem is not null)
            return OperationResult<SettlementPeriod>.Fail(problem);

        return OperationResult<SettlementPeriod>.Success(await CreatePeriodAsync(customerId, seasonId, from, toDate, user, ct));
    }

    /// <summary>Takes back the newest period, only while nothing has been paid against it.</summary>
    public async Task<OperationResult> UndoLastCloseAsync(int customerId, int seasonId, CancellationToken ct = default)
    {
        if (await db.Seasons.AnyAsync(s => s.Id == seasonId && s.IsClosed, ct))
            return OperationResult.Fail("Mùa này đã chốt, không hủy chốt kỳ được.");

        var last = (await db.Periods.Where(p => p.CustomerId == customerId && p.SeasonId == seasonId).ToListAsync(ct))
            .OrderByDescending(p => p.FromDate).FirstOrDefault();
        if (last is null)
            return OperationResult.Fail("Chưa có kỳ nào để hủy chốt.");

        var attached = await db.Payments.Where(p => p.PeriodId == last.Id).ToListAsync(ct);
        if (attached.Any(p => !p.IsVoided))
            return OperationResult.Fail("Kỳ này đã có khoản trả. Hãy hủy các khoản trả đó trước khi hủy chốt kỳ.");

        foreach (var voided in attached)
            voided.PeriodId = null;
        db.Periods.Remove(last);
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }

    public async Task<OperationResult<IReadOnlyList<BatchRow>>> PreviewBatchAsync(
        Season season, DateOnly toDate, CancellationToken ct = default)
    {
        var rows = await BuildBatchAsync(season, toDate, ct);
        return OperationResult<IReadOnlyList<BatchRow>>.Success(rows);
    }

    /// <summary>Closes the open period of every seller who has trucks up to <paramref name="toDate"/>.</summary>
    public async Task<OperationResult<int>> CloseBatchAsync(int seasonId, DateOnly toDate, string user, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId, ct);
        if (season is null)
            return OperationResult<int>.Fail("Không tìm thấy mùa.");
        if (season.IsClosed)
            return OperationResult<int>.Fail("Mùa này đã chốt.");
        if (toDate > DateOnly.FromDateTime(DateTime.Today))
            return OperationResult<int>.Fail("Không chốt kỳ đến một ngày trong tương lai.");

        var rows = await BuildBatchAsync(season, toDate, ct);
        foreach (var row in rows)
            await CreatePeriodAsync(row.CustomerId, seasonId, row.From, toDate, user, ct);
        return OperationResult<int>.Success(rows.Count);
    }

    private async Task<SettlementPeriod> CreatePeriodAsync(
        int customerId, int seasonId, DateOnly from, DateOnly to, string user, CancellationToken ct)
    {
        var period = new SettlementPeriod
        {
            CustomerId = customerId, SeasonId = seasonId, FromDate = from, ToDate = to,
            ClosedAt = DateTimeOffset.Now, ClosedBy = user
        };
        db.Periods.Add(period);
        await db.SaveChangesAsync(ct);

        // Money paid ahead, before this period existed, now counts toward it.
        foreach (var ahead in await db.Payments
                     .Where(p => p.CustomerId == customerId && p.SeasonId == seasonId && p.PeriodId == null && p.VoidedAt == null)
                     .ToListAsync(ct))
            ahead.PeriodId = period.Id;
        await db.SaveChangesAsync(ct);
        return period;
    }

    private async Task<IReadOnlyList<BatchRow>> BuildBatchAsync(Season season, DateOnly toDate, CancellationToken ct)
    {
        var policy = await new SettingsService(db).GetBilledCargoPolicyAsync(ct);
        var customers = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var allTickets = (await db.Tickets.AsNoTracking()
                .Where(t => !t.IsDeleted && t.CustomerId != null && t.TicketDate >= season.StartDate && t.TicketDate <= toDate)
                .ToListAsync(ct))
            .Where(t => policy.IsBilled(t.Cargo))
            .ToList();
        var periods = (await db.Periods.AsNoTracking().Where(p => p.SeasonId == season.Id).ToListAsync(ct))
            .ToLookup(p => p.CustomerId);
        var payments = (await db.Payments.AsNoTracking().Where(p => p.SeasonId == season.Id && p.VoidedAt == null).ToListAsync(ct))
            .ToLookup(p => p.CustomerId);

        var rows = new List<BatchRow>();
        foreach (var group in allTickets.GroupBy(t => t.CustomerId!.Value))
        {
            var customerPeriods = periods[group.Key].OrderBy(p => p.FromDate).ToList();
            var from = customerPeriods.Count == 0 ? season.StartDate : customerPeriods[^1].ToDate.AddDays(1);
            if (toDate < from)
                continue;

            var inRange = group.Where(t => t.TicketDate >= from).ToList();
            if (inRange.Count == 0)
                continue;

            var settlement = PeriodBuilder.Build(season, group, customerPeriods, payments[group.Key], new Dictionary<string, string>());
            var carry = settlement.Closed.LastOrDefault()?.Remaining ?? 0;
            var total = inRange.Where(t => t.Stage == TicketStage.Priced).Sum(t => t.AmountVnd ?? 0);
            rows.Add(new BatchRow(
                group.Key, customers.GetValueOrDefault(group.Key, "(đã xóa)"), from, inRange.Count,
                inRange.Count(t => t.Stage != TicketStage.Priced), total, carry, carry + total));
        }

        return rows.OrderBy(r => r.CustomerName).ToList();
    }

    private static string? ValidateClose(
        Season season, IReadOnlyList<SettlementPeriod> periods, IReadOnlyList<TicketRecord> tickets, DateOnly toDate, out DateOnly from)
    {
        var last = periods.OrderBy(p => p.FromDate).LastOrDefault();
        var start = last is null ? season.StartDate : last.ToDate.AddDays(1);
        from = start;

        if (season.IsClosed)
            return "Mùa này đã chốt, không chốt thêm kỳ được.";
        if (toDate < start)
            return $"Ngày chốt phải từ {start:dd/MM/yyyy} trở đi (sau kỳ trước).";
        if (toDate > DateOnly.FromDateTime(DateTime.Today))
            return "Không chốt kỳ đến một ngày trong tương lai.";
        if (!tickets.Any(t => !t.IsDeleted && t.TicketDate >= start && t.TicketDate <= toDate))
            return "Không có xe nào trong khoảng ngày này.";
        return null;
    }

    private async Task<(List<TicketRecord> Tickets, List<SettlementPeriod> Periods, List<Payment> Payments, Dictionary<string, string> Stations)>
        LoadAsync(int customerId, Season season, CancellationToken ct)
    {
        var policy = await new SettingsService(db).GetBilledCargoPolicyAsync(ct);
        var start = season.StartDate;
        var end = season.EndDate;
        var tickets = (await db.Tickets.AsNoTracking()
                .Where(t => t.CustomerId == customerId && !t.IsDeleted && t.TicketDate >= start && (end == null || t.TicketDate <= end))
                .ToListAsync(ct))
            .Where(t => policy.IsBilled(t.Cargo))
            .ToList();
        var periods = await db.Periods.AsNoTracking().Where(p => p.CustomerId == customerId && p.SeasonId == season.Id).ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking().Where(p => p.CustomerId == customerId && p.SeasonId == season.Id).ToListAsync(ct);
        var stations = await new LedgerService(db).GetStationNamesAsync(ct);
        return (tickets, periods, payments, stations);
    }
}
