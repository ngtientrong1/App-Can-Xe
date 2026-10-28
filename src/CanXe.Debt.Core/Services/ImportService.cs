using CanXe.Debt.Contract;
using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

public sealed record ImportResult(
    int BatchId,
    string StationCode,
    string StationName,
    DateOnly? From,
    DateOnly? To,
    int Total,
    int Added,
    int Updated,
    int Unchanged,
    int SkippedClosedSeason,
    int OutsideSeason,
    int UnmappedTickets,
    int PendingPrice,
    IReadOnlyList<string> Warnings,
    int AwaitingSecondWeigh = 0);

public sealed class ImportService(DebtDbContext db)
{
    public const int MaxTickets = 200_000;

    /// <summary>
    /// Merges an export file into the ledger. A ticket is identified by station + ticket code, so importing the same
    /// file twice, or overlapping files, never duplicates anything: changed tickets are updated, the rest left alone.
    /// </summary>
    public async Task<OperationResult<ImportResult>> ImportAsync(
        DebtExportFile file, string fileName, string importedBy, CancellationToken ct = default)
    {
        var problem = Validate(file);
        if (problem is not null)
            return OperationResult<ImportResult>.Fail(problem);

        var stationCode = file.Station.Code.Trim().ToUpperInvariant();
        var stationName = string.IsNullOrWhiteSpace(file.Station.Name) ? stationCode : file.Station.Name.Trim();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var seasons = await db.Seasons.AsNoTracking().ToListAsync(ct);
        var lookup = await new CustomerService(db).BuildLookupAsync(ct);
        var existing = await db.Tickets.Where(t => t.StationCode == stationCode).ToDictionaryAsync(t => t.Code, ct);
        var warnings = BuildWarnings(file, stationCode, await db.ImportBatches.AsNoTracking().Where(b => b.StationCode == stationCode).ToListAsync(ct));

        var dates = file.Tickets.Select(t => DateOnly.FromDateTime(t.DateTime.DateTime)).ToList();
        var batch = new ImportBatch
        {
            StationCode = stationCode,
            StationName = stationName,
            FileName = fileName,
            ExportedAt = file.ExportedAt,
            ImportedAt = DateTimeOffset.Now,
            ImportedBy = importedBy,
            FromDate = dates.Count > 0 ? dates.Min() : null,
            ToDate = dates.Count > 0 ? dates.Max() : null,
            TicketCount = file.Tickets.Count
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);

        int added = 0, updated = 0, unchanged = 0, skipped = 0, outside = 0;
        foreach (var source in file.Tickets)
        {
            var date = DateOnly.FromDateTime(source.DateTime.DateTime);
            var season = seasons.FirstOrDefault(s => s.Contains(date));
            if (season is null)
                outside++;
            var frozen = season?.IsClosed == true;

            var hash = DebtExportSerializer.ComputeTicketHash(source);
            var normalized = NameNormalizer.Normalize(source.Customer);

            if (ServiceWeigh.IsServiceCustomer(source.Customer))
            {
                // Weighing someone else's truck is no purchase: it never enters the ledger, and one imported before is dropped.
                if (existing.TryGetValue(source.Code, out var serviceTicket) && !serviceTicket.IsDeleted)
                {
                    serviceTicket.IsDeleted = true;
                    serviceTicket.PeriodId = null;
                    serviceTicket.UpdatedAt = DateTimeOffset.Now;
                    updated++;
                }

                continue;
            }

            if (!existing.TryGetValue(source.Code, out var ticket))
            {
                if (source.IsDeleted)
                    continue;
                if (frozen)
                {
                    skipped++;
                    continue;
                }

                ticket = new TicketRecord
                {
                    StationCode = stationCode,
                    Code = source.Code,
                    DisplayNumber = source.DisplayNumber,
                    ContentHash = hash
                };
                Apply(ticket, source, date, normalized, lookup, batch.Id);
                db.Tickets.Add(ticket);
                existing[source.Code] = ticket;
                added++;
                continue;
            }

            if (ticket.ContentHash == hash)
            {
                unchanged++;
                continue;
            }

            if (frozen || seasons.FirstOrDefault(s => s.Contains(ticket.TicketDate))?.IsClosed == true)
            {
                skipped++;
                continue;
            }

            ticket.ContentHash = hash;
            Apply(ticket, source, date, normalized, lookup, batch.Id);
            updated++;
        }

        batch.Added = added;
        batch.Updated = updated;
        batch.Unchanged = unchanged;
        batch.Skipped = skipped;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var fileCodes = file.Tickets.Select(t => t.Code).ToHashSet();
        var touched = existing.Values.Where(t => fileCodes.Contains(t.Code) && !t.IsDeleted).ToList();
        var policy = await new SettingsService(db).GetBilledCargoPolicyAsync(ct);
        var billable = touched.Where(t => policy.IsBilled(t.Cargo)).ToList();

        return OperationResult<ImportResult>.Success(new ImportResult(
            batch.Id, stationCode, stationName, batch.FromDate, batch.ToDate, file.Tickets.Count,
            added, updated, unchanged, skipped, outside,
            touched.Count(t => t.CustomerId is null),
            billable.Count(t => t.Stage == TicketStage.NoPrice),
            warnings,
            billable.Count(t => t.Stage == TicketStage.AwaitingSecondWeigh)));
    }

    /// <summary>Drops the "Cân Dịch Vụ" tickets imported before they were excluded. The rows stay in the file as deleted, so nothing is lost for good.</summary>
    public static async Task<int> PurgeServiceWeighsAsync(DebtDbContext db, CancellationToken ct = default)
    {
        var service = await db.Tickets
            .Where(t => !t.IsDeleted && t.CustomerNameNormalized == "can dich vu")
            .ToListAsync(ct);
        foreach (var ticket in service)
        {
            ticket.IsDeleted = true;
            ticket.PeriodId = null;
            ticket.UpdatedAt = DateTimeOffset.Now;
        }

        if (service.Count > 0)
            await db.SaveChangesAsync(ct);
        return service.Count;
    }

    public static string? Validate(DebtExportFile? file)
    {
        if (file is null || file.Format != DebtExportFile.FormatId)
            return "Đây không phải file xuất công nợ của app cân.";
        if (file.Version > DebtExportFile.CurrentVersion)
            return "File này do bản app cân mới hơn tạo ra. Hãy cập nhật app công nợ.";
        if (string.IsNullOrWhiteSpace(file.Station?.Code))
            return "File thiếu mã điểm cân.";
        if (file.Tickets is null)
            return "File không có danh sách phiếu.";
        if (file.Tickets.Count > MaxTickets)
            return "File có quá nhiều phiếu.";
        if (!file.IsChecksumValid)
            return "File bị hỏng hoặc đã bị sửa (mã kiểm tra không khớp). Hãy xin lại file từ app cân.";

        var seen = new HashSet<string>();
        foreach (var ticket in file.Tickets)
        {
            if (string.IsNullOrWhiteSpace(ticket.Code))
                return "File có phiếu thiếu mã phiếu.";
            if (!seen.Add(ticket.Code))
                return $"File có mã phiếu bị lặp: {ticket.Code}.";
        }

        return null;
    }

    private static List<string> BuildWarnings(DebtExportFile file, string stationCode, IReadOnlyList<ImportBatch> previous)
    {
        var warnings = new List<string>();
        if (file.Tickets.Count == 0)
        {
            warnings.Add("File không có phiếu nào.");
            return warnings;
        }

        if (previous.Count == 0)
            return warnings;

        var latest = previous.OrderByDescending(b => b.ExportedAt).First();
        if (file.ExportedAt < latest.ExportedAt)
            warnings.Add($"File này xuất lúc {file.ExportedAt:dd/MM/yyyy HH:mm}, cũ hơn file đã nhập trước đó của {stationCode} ({latest.ExportedAt:dd/MM/yyyy HH:mm}).");

        var covered = previous.Where(b => b.ToDate is not null).Select(b => b.ToDate!.Value).DefaultIfEmpty().Max();
        var from = file.Tickets.Min(t => DateOnly.FromDateTime(t.DateTime.DateTime));
        if (covered != default && from > covered.AddDays(1))
            warnings.Add($"Có thể bị hở ngày: lần nhập trước của {stationCode} đến {covered:dd/MM/yyyy}, file này bắt đầu từ {from:dd/MM/yyyy}.");

        return warnings;
    }

    private static void Apply(
        TicketRecord ticket, ExportedTicket source, DateOnly date, string normalizedName,
        IReadOnlyDictionary<string, int> lookup, int batchId)
    {
        ticket.DisplayNumber = source.DisplayNumber;
        ticket.TicketDate = date;
        ticket.TicketDateTime = source.DateTime;
        ticket.CustomerNameRaw = source.Customer?.Trim();
        ticket.CustomerNameNormalized = normalizedName.Length > 0 ? normalizedName : null;
        ticket.CustomerId = normalizedName.Length > 0 && lookup.TryGetValue(normalizedName, out var customerId) ? customerId : null;
        ticket.LicensePlate = source.LicensePlate;
        ticket.Cargo = source.Cargo;
        ticket.GrossWeightKg = source.GrossWeightKg;
        ticket.TareWeightKg = source.TareWeightKg;
        ticket.NetWeightKg = source.NetWeightKg;
        ticket.BillableWeightKg = source.BillableWeightKg;
        ticket.Notes = string.IsNullOrWhiteSpace(source.Notes) ? null : source.Notes.Trim();
        ticket.AwaitingSecondWeigh = source.AwaitingSecondWeigh;
        ticket.FirstWeightKg = source.FirstWeightKg;
        ticket.UnitPrice = source.UnitPrice;
        ticket.SplitFirstWeightKg = source.SplitFirstWeightKg;
        ticket.SecondUnitPrice = source.SecondUnitPrice;
        ticket.AmountVnd = source.AmountVnd;
        ticket.IsDeleted = source.IsDeleted;

        // A ticket an admin corrected keeps the correction: the file only refreshes what the weighing app said.
        if (ticket.IsAdjusted && !source.AwaitingSecondWeigh && source.NetWeightKg is not null)
        {
            ticket.OriginalNetKg = source.NetWeightKg;
            ticket.OriginalBillableKg = source.BillableWeightKg;
            ticket.OriginalUnitPrice = source.UnitPrice;
            ticket.OriginalAmountVnd = source.AmountVnd;
            TicketPricing.Reapply(ticket);
        }

        ticket.ImportBatchId = batchId;
        ticket.UpdatedAt = DateTimeOffset.Now;
    }
}
