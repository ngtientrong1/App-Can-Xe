using System.Text;
using CanXe.Application.Interfaces;
using CanXe.Application.Mapping;
using CanXe.Application.Models;
using CanXe.Debt.Contract;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Infrastructure.Services;

public sealed class DebtExportService(CanXeDbContext db) : IDebtExportService
{
    public async Task<DebtExportResult> ExportAsync(
        DateTime fromDate,
        DateTime toDate,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var station = await db.StationSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            var code = DebtStationCodePolicy.Normalize(station?.DebtStationCode);
            if (!DebtStationCodePolicy.IsValid(code))
            {
                return DebtExportResult.Failed(
                    "Chưa đặt mã điểm cân. Vào Cài đặt → Cấu hình trạm cân, mục “Điểm cân”, nhập mã (ví dụ DAKHA) rồi lưu.");
            }

            var name = string.IsNullOrWhiteSpace(station?.DebtStationName) ? code! : station!.DebtStationName!.Trim();
            var tickets = (await db.WeighTickets.AsNoTracking().Include(t => t.Events).ToListAsync(cancellationToken))
                .Where(t => t.TicketDateTime.Date >= fromDate.Date && t.TicketDateTime.Date <= toDate.Date)
                .ToList();

            var file = BuildFile(new StationInfo(code!, name), tickets, DateTimeOffset.Now);
            await WriteAtomicallyAsync(filePath, DebtExportSerializer.Serialize(file), cancellationToken);

            return new DebtExportResult(
                true, null, filePath, file.Tickets.Count(t => !t.IsDeleted), file.Tickets.Count(t => t.IsDeleted), code, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return DebtExportResult.Failed($"Không ghi được file: {ex.Message}");
        }
    }

    /// <summary>
    /// Every ticket that has at least one weighing is exported, so the debt app can show a truck from its first
    /// weighing on: while it waits for the empty-truck weighing, and while it has no price. Re-exporting later
    /// carries the finished weights and price, and the debt app updates the same ticket (it never duplicates).
    /// A ticket deleted at the station is exported flagged so the debt app drops it. Tickets with no weighing at
    /// all are drafts and are left out.
    /// </summary>
    public static DebtExportFile BuildFile(StationInfo station, IEnumerable<WeighTicket> tickets, DateTimeOffset exportedAt)
    {
        var rows = tickets
            .Where(t => t.IsDeleted || t.Events.Count > 0)
            .OrderBy(t => t.TicketDateTime)
            .ThenBy(t => t.InternalCode, StringComparer.Ordinal)
            .Select(Map)
            .ToList();
        return DebtExportFile.Create(station, exportedAt, rows);
    }

    private static ExportedTicket Map(WeighTicket t)
    {
        var state = WeighTicketWorkflow.FromEventCount(t.Events.Count);
        var awaiting = !t.IsDeleted && state == WeighTicketWorkflowState.AwaitingSecondWeigh;
        var first = t.Events.OrderBy(e => e.Sequence).FirstOrDefault();
        var firstKg = first is null ? null : WeightStorageMapper.FromGrams(WeighEventWeightResolver.GetEffectiveWeightGrams(first));

        // With only the first weighing the ticket's gross/net would just repeat that weight, which is not a real net.
        return new ExportedTicket(
            t.InternalCode,
            t.DisplayNumber,
            t.TicketDateTime,
            t.CustomerNameSnapshot,
            t.LicensePlateSnapshot,
            t.CargoTypeNameSnapshot,
            awaiting ? null : WeightStorageMapper.FromGrams(t.GrossWeightGrams),
            awaiting ? null : WeightStorageMapper.FromGrams(t.TareWeightGrams),
            awaiting ? null : WeightStorageMapper.FromGrams(t.NetWeightGrams),
            awaiting ? null : WeightStorageMapper.FromGrams(t.BillableWeightGrams),
            t.UnitPriceVndPerKg,
            WeightStorageMapper.FromGrams(t.SplitFirstWeightGrams),
            t.SecondUnitPriceVndPerKg,
            awaiting || t.UnitPriceVndPerKg is null ? null : t.TotalAmountVnd,
            t.IsDeleted,
            awaiting,
            firstKg);
    }

    private static async Task WriteAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content, new UTF8Encoding(false), cancellationToken);
        File.Move(temp, path, overwrite: true);
    }
}
