using System.Text;
using CanXe.Application.Interfaces;
using CanXe.Application.Mapping;
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
            var tickets = (await db.WeighTickets.AsNoTracking().ToListAsync(cancellationToken))
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
    /// Completed tickets are exported with their money; a ticket deleted at the station is exported flagged so the debt
    /// app drops it. Tickets still waiting for their second weighing are left out until they are complete.
    /// </summary>
    public static DebtExportFile BuildFile(StationInfo station, IEnumerable<WeighTicket> tickets, DateTimeOffset exportedAt)
    {
        var rows = tickets
            .Where(t => t.IsDeleted || t.BillableWeightGrams is not null)
            .OrderBy(t => t.TicketDateTime)
            .ThenBy(t => t.InternalCode, StringComparer.Ordinal)
            .Select(Map)
            .ToList();
        return DebtExportFile.Create(station, exportedAt, rows);
    }

    private static ExportedTicket Map(WeighTicket t) =>
        new(
            t.InternalCode,
            t.DisplayNumber,
            t.TicketDateTime,
            t.CustomerNameSnapshot,
            t.LicensePlateSnapshot,
            t.CargoTypeNameSnapshot,
            WeightStorageMapper.FromGrams(t.GrossWeightGrams),
            WeightStorageMapper.FromGrams(t.TareWeightGrams),
            WeightStorageMapper.FromGrams(t.NetWeightGrams),
            WeightStorageMapper.FromGrams(t.BillableWeightGrams),
            t.UnitPriceVndPerKg,
            WeightStorageMapper.FromGrams(t.SplitFirstWeightGrams),
            t.SecondUnitPriceVndPerKg,
            t.UnitPriceVndPerKg is null ? null : t.TotalAmountVnd,
            t.IsDeleted);

    private static async Task WriteAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content, new UTF8Encoding(false), cancellationToken);
        File.Move(temp, path, overwrite: true);
    }
}
