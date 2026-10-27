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

    /// <summary>What closing a period out of the chosen trucks would look like, without saving it.</summary>
    public async Task<OperationResult<PeriodView>> PreviewCloseAsync(
        int customerId, Season season, IReadOnlyCollection<int> ticketIds, CancellationToken ct = default)
    {
        var data = await LoadAsync(customerId, season, ct);
        var problem = ValidateSelection(season, data.Tickets, ticketIds, out var chosen);
        if (problem is not null)
            return OperationResult<PeriodView>.Fail(problem);

        const int hypotheticalId = -1;
        var chosenIds = chosen.Select(t => t.Id).ToHashSet();
        var hypothetical = new SettlementPeriod
        {
            Id = hypotheticalId, CustomerId = customerId, SeasonId = season.Id,
            FromDate = chosen.Min(t => t.TicketDate), ToDate = chosen.Max(t => t.TicketDate),
            ClosedAt = DateTimeOffset.Now, ClosedBy = ""
        };
        var settlement = PeriodBuilder.Build(
            season, data.Tickets, data.Periods.Append(hypothetical), data.Payments, data.Stations,
            p => p.PeriodId ?? hypotheticalId,
            t => chosenIds.Contains(t.Id) ? hypotheticalId : t.PeriodId);
        return OperationResult<PeriodView>.Success(settlement.Periods.First(p => p.Id == hypotheticalId));
    }

    /// <summary>Gathers the chosen trucks into a period. Any number of trucks, down to a single one, in any combination.</summary>
    public async Task<OperationResult<SettlementPeriod>> ClosePeriodAsync(
        int customerId, int seasonId, IReadOnlyCollection<int> ticketIds, string user, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId, ct);
        if (season is null)
            return OperationResult<SettlementPeriod>.Fail("Không tìm thấy mùa.");
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            return OperationResult<SettlementPeriod>.Fail("Không tìm thấy chủ hàng.");

        var data = await LoadAsync(customerId, season, ct);
        var problem = ValidateSelection(season, data.Tickets, ticketIds, out var chosen);
        if (problem is not null)
            return OperationResult<SettlementPeriod>.Fail(problem);

        return OperationResult<SettlementPeriod>.Success(await CreatePeriodAsync(customerId, seasonId, chosen.Select(t => t.Id).ToList(), user, ct));
    }

    /// <summary>Takes back the newest period, only while nothing has been paid against it. Its trucks go back to waiting.</summary>
    public async Task<OperationResult> UndoLastCloseAsync(int customerId, int seasonId, CancellationToken ct = default)
    {
        if (await db.Seasons.AnyAsync(s => s.Id == seasonId && s.IsClosed, ct))
            return OperationResult.Fail("Mùa này đã chốt, không hủy chốt lô được.");

        var last = (await db.Periods.Where(p => p.CustomerId == customerId && p.SeasonId == seasonId).ToListAsync(ct))
            .OrderByDescending(p => p.Id).FirstOrDefault();
        if (last is null)
            return OperationResult.Fail("Chưa có lô nào để hủy chốt.");

        var attached = await db.Payments.Where(p => p.PeriodId == last.Id).ToListAsync(ct);
        if (attached.Any(p => !p.IsVoided))
            return OperationResult.Fail("Lô này đã có khoản trả. Hãy hủy các khoản trả đó trước khi hủy chốt lô.");

        foreach (var voided in attached)
            voided.PeriodId = null;
        foreach (var ticket in await db.Tickets.Where(t => t.PeriodId == last.Id).ToListAsync(ct))
            ticket.PeriodId = null;
        db.Periods.Remove(last);
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }

    public async Task<OperationResult<IReadOnlyList<BatchRow>>> PreviewBatchAsync(
        Season season, DateOnly toDate, CancellationToken ct = default)
    {
        var rows = await BuildBatchAsync(season, toDate, ct);
        return OperationResult<IReadOnlyList<BatchRow>>.Success(rows.Select(r => r.Row).ToList());
    }

    /// <summary>For every seller, gathers all trucks still waiting up to <paramref name="toDate"/> into one period.</summary>
    public async Task<OperationResult<int>> CloseBatchAsync(int seasonId, DateOnly toDate, string user, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId, ct);
        if (season is null)
            return OperationResult<int>.Fail("Không tìm thấy mùa.");
        if (season.IsClosed)
            return OperationResult<int>.Fail("Mùa này đã chốt.");
        if (toDate > DateOnly.FromDateTime(DateTime.Today))
            return OperationResult<int>.Fail("Không chốt lô đến một ngày trong tương lai.");

        var rows = await BuildBatchAsync(season, toDate, ct);
        foreach (var (row, ids) in rows)
            await CreatePeriodAsync(row.CustomerId, seasonId, ids, user, ct);
        return OperationResult<int>.Success(rows.Count);
    }

    private async Task<SettlementPeriod> CreatePeriodAsync(
        int customerId, int seasonId, IReadOnlyCollection<int> ticketIds, string user, CancellationToken ct)
    {
        var tickets = await db.Tickets.Where(t => ticketIds.Contains(t.Id)).ToListAsync(ct);
        var period = new SettlementPeriod
        {
            CustomerId = customerId, SeasonId = seasonId,
            FromDate = tickets.Min(t => t.TicketDate), ToDate = tickets.Max(t => t.TicketDate),
            ClosedAt = DateTimeOffset.Now, ClosedBy = user
        };
        db.Periods.Add(period);
        await db.SaveChangesAsync(ct);

        foreach (var ticket in tickets)
            ticket.PeriodId = period.Id;

        // Money paid ahead, before this period existed, now counts toward it.
        foreach (var ahead in await db.Payments
                     .Where(p => p.CustomerId == customerId && p.SeasonId == seasonId && p.PeriodId == null && p.VoidedAt == null)
                     .ToListAsync(ct))
            ahead.PeriodId = period.Id;
        await db.SaveChangesAsync(ct);
        return period;
    }

    private async Task<List<(BatchRow Row, List<int> TicketIds)>> BuildBatchAsync(Season season, DateOnly toDate, CancellationToken ct)
    {
        var policy = await new SettingsService(db).GetBilledCargoPolicyAsync(ct);
        var customers = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var start = season.StartDate;
        var seasonEnd = season.EndDate;
        var all = (await db.Tickets.AsNoTracking()
                .Where(t => !t.IsDeleted && t.CustomerId != null && t.TicketDate >= start && (seasonEnd == null || t.TicketDate <= seasonEnd))
                .ToListAsync(ct))
            .Where(t => policy.IsBilled(t.Cargo))
            .ToList();
        var periods = (await db.Periods.AsNoTracking().Where(p => p.SeasonId == season.Id).ToListAsync(ct)).ToLookup(p => p.CustomerId);
        var payments = (await db.Payments.AsNoTracking().Where(p => p.SeasonId == season.Id && p.VoidedAt == null).ToListAsync(ct))
            .ToLookup(p => p.CustomerId);

        var rows = new List<(BatchRow, List<int>)>();
        foreach (var group in all.GroupBy(t => t.CustomerId!.Value))
        {
            var waiting = group.Where(t => t.PeriodId is null && t.TicketDate <= toDate).ToList();
            if (waiting.Count == 0)
                continue;

            var settlement = PeriodBuilder.Build(season, group, periods[group.Key], payments[group.Key], new Dictionary<string, string>());
            var carry = settlement.Closed.LastOrDefault()?.Remaining ?? 0;
            var total = waiting.Where(t => t.Stage == TicketStage.Priced).Sum(t => t.AmountVnd ?? 0);
            rows.Add((new BatchRow(
                group.Key, customers.GetValueOrDefault(group.Key, "(đã xóa)"), waiting.Min(t => t.TicketDate), waiting.Count,
                waiting.Count(t => t.Stage != TicketStage.Priced), total, carry, carry + total),
                waiting.Select(t => t.Id).ToList()));
        }

        return rows.OrderBy(r => r.Item1.CustomerName).ToList();
    }

    private static string? ValidateSelection(
        Season season, IReadOnlyList<TicketRecord> tickets, IReadOnlyCollection<int> ticketIds, out List<TicketRecord> chosen)
    {
        chosen = [];
        if (season.IsClosed)
            return "Mùa này đã chốt, không chốt thêm lô được.";

        var ids = ticketIds.Distinct().ToList();
        if (ids.Count == 0)
            return "Chưa chọn xe nào để chốt.";

        var waiting = tickets.Where(t => !t.IsDeleted && t.PeriodId is null).ToDictionary(t => t.Id);
        if (ids.Any(id => !waiting.ContainsKey(id)))
            return "Có xe không thuộc chủ hàng này hoặc đã nằm trong một lô khác. Tải lại trang rồi chọn lại.";

        chosen = ids.Select(id => waiting[id]).OrderBy(t => t.TicketDateTime).ThenBy(t => t.StationCode).ThenBy(t => t.Code).ToList();
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
