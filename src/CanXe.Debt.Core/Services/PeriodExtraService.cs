using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

/// <summary>Extra fees on a lot: a truck fare or a payment for short goods that is paid to the seller on top of the trucks.</summary>
public sealed class PeriodExtraService(DebtDbContext db)
{
    public static readonly string[] CommonLabels = ["Tiền xe", "Tiền bù hàng", "Tiền bốc xếp", "Phụ phí khác"];

    /// <summary>Adds an extra fee to a closed lot. A negative amount takes something off what the lot has to pay.</summary>
    public async Task<OperationResult<PeriodExtra>> AddAsync(
        int customerId, int seasonId, int periodId, DateOnly date, string? label, long amountVnd, string? note, string user,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(label))
            return OperationResult<PeriodExtra>.Fail("Nhập khoản phụ phí là gì (ví dụ: Tiền xe).");
        if (amountVnd == 0)
            return OperationResult<PeriodExtra>.Fail("Số tiền phụ phí không được bằng 0.");

        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId, ct);
        if (season is null)
            return OperationResult<PeriodExtra>.Fail("Không tìm thấy mùa.");
        if (season.IsClosed)
            return OperationResult<PeriodExtra>.Fail("Mùa này đã chốt, không thêm phụ phí được.");
        if (!season.Contains(date))
            return OperationResult<PeriodExtra>.Fail($"Ngày phải từ {season.StartDate:dd/MM/yyyy} trở đi.");
        if (!await db.Periods.AnyAsync(p => p.Id == periodId && p.CustomerId == customerId && p.SeasonId == seasonId, ct))
            return OperationResult<PeriodExtra>.Fail("Phụ phí phải gắn vào một lô đã chốt của chủ hàng này. Hãy gom xe thành lô trước.");

        var extra = new PeriodExtra
        {
            CustomerId = customerId,
            SeasonId = seasonId,
            PeriodId = periodId,
            Date = date,
            Label = label.Trim(),
            AmountVnd = amountVnd,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedBy = user,
            CreatedAt = DateTimeOffset.Now
        };
        db.PeriodExtras.Add(extra);
        await db.SaveChangesAsync(ct);
        return OperationResult<PeriodExtra>.Success(extra);
    }

    /// <summary>Like a payment, an extra fee is never deleted: voiding keeps it on record with who, when and why.</summary>
    public async Task<OperationResult> VoidAsync(int extraId, string? reason, string user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return OperationResult.Fail("Nhập lý do hủy phụ phí.");

        var extra = await db.PeriodExtras.FirstOrDefaultAsync(e => e.Id == extraId, ct);
        if (extra is null)
            return OperationResult.Fail("Không tìm thấy phụ phí.");
        if (extra.IsVoided)
            return OperationResult.Fail("Phụ phí này đã hủy rồi.");
        if (await db.Seasons.AnyAsync(s => s.Id == extra.SeasonId && s.IsClosed, ct))
            return OperationResult.Fail("Mùa này đã chốt, không hủy được phụ phí.");

        extra.VoidedAt = DateTimeOffset.Now;
        extra.VoidedBy = user;
        extra.VoidReason = reason.Trim();
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }
}
