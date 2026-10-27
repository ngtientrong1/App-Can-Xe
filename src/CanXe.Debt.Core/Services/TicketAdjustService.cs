using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

/// <summary>The weighing app's money rules, so a corrected weight is priced exactly as the app would have priced it.</summary>
public static class TicketPricing
{
    public const decimal DeductionPerMille = 3m;

    public static decimal BillableOf(decimal netKg) =>
        Math.Round(netKg - netKg / 1000m * DeductionPerMille, 0, MidpointRounding.AwayFromZero);

    /// <summary>Weight paid for and amount; with a two-level price each part loses its own 3‰ and is rounded on its own.</summary>
    public static (decimal BillableKg, long? Amount) Price(decimal netKg, decimal? unitPrice, decimal? splitFirstKg, decimal? secondPrice)
    {
        static long Line(decimal kg, decimal price) => (long)Math.Round(kg * price, 0, MidpointRounding.AwayFromZero);

        if (splitFirstKg is > 0 && secondPrice is not null && splitFirstKg < netKg)
        {
            var first = BillableOf(splitFirstKg.Value);
            var second = BillableOf(netKg - splitFirstKg.Value);
            return (first + second, unitPrice is { } p ? Line(first, p) + Line(second, secondPrice.Value) : null);
        }

        var billable = BillableOf(netKg);
        return (billable, unitPrice is { } price ? Line(billable, price) : null);
    }

    /// <summary>Sets the ticket's weight and money from its Original* values with the admin's correction (if any) on top.</summary>
    public static void Reapply(TicketRecord ticket)
    {
        var net = ticket.AdjustedNetKg ?? ticket.OriginalNetKg;
        var price = ticket.AdjustedUnitPrice ?? ticket.OriginalUnitPrice;
        if (!ticket.IsAdjusted || net is null)
        {
            ticket.NetWeightKg = ticket.OriginalNetKg;
            ticket.BillableWeightKg = ticket.OriginalBillableKg;
            ticket.UnitPrice = ticket.OriginalUnitPrice;
            ticket.AmountVnd = ticket.OriginalAmountVnd;
            return;
        }

        var (billable, amount) = Price(net.Value, price, ticket.SplitFirstWeightKg, ticket.SecondUnitPrice);
        ticket.NetWeightKg = net;
        ticket.BillableWeightKg = billable;
        ticket.UnitPrice = price;
        ticket.AmountVnd = amount;
    }
}

public sealed class TicketAdjustService(DebtDbContext db)
{
    /// <summary>
    /// Admin correction of a ticket already imported: the weight paid for (and the unit price, for a single-price ticket).
    /// The weighing app's own numbers are kept, shown beside the correction and restored on request; a later re-import
    /// of the file keeps the correction. Every change is logged with who, when and why.
    /// </summary>
    public async Task<OperationResult<TicketRecord>> AdjustAsync(
        int ticketId, decimal netKg, decimal? unitPrice, string? reason, string user, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return OperationResult<TicketRecord>.Fail("Nhập lý do chỉnh (ví dụ: lấy theo cân kho khách).");
        if (netKg <= 0)
            return OperationResult<TicketRecord>.Fail("Khối lượng phải lớn hơn 0.");
        if (unitPrice is <= 0)
            return OperationResult<TicketRecord>.Fail("Đơn giá phải lớn hơn 0.");

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        var problem = await CheckEditableAsync(ticket, ct);
        if (problem is not null)
            return OperationResult<TicketRecord>.Fail(problem);

        if (ticket.HasPriceSplit && unitPrice is not null && unitPrice != ticket.AdjustedUnitPrice && unitPrice != (ticket.OriginalUnitPrice ?? ticket.UnitPrice))
            return OperationResult<TicketRecord>.Fail("Phiếu này có 2 mức giá nên chỉ chỉnh được khối lượng.");

        var wasAdjusted = ticket.IsAdjusted;
        if (!wasAdjusted)
        {
            ticket.OriginalNetKg = ticket.NetWeightKg;
            ticket.OriginalBillableKg = ticket.BillableWeightKg;
            ticket.OriginalUnitPrice = ticket.UnitPrice;
            ticket.OriginalAmountVnd = ticket.AmountVnd;
        }

        var before = (Net: ticket.NetWeightKg, Price: ticket.UnitPrice);
        var newPrice = ticket.HasPriceSplit ? (decimal?)null : unitPrice;
        ticket.AdjustedNetKg = netKg == ticket.OriginalNetKg ? null : netKg;
        ticket.AdjustedUnitPrice = newPrice is { } p && p != ticket.OriginalUnitPrice ? p : null;
        TicketPricing.Reapply(ticket);

        var changes = new List<string>();
        var vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        if (before.Net != ticket.NetWeightKg)
            changes.Add($"Khối lượng hàng: {before.Net?.ToString("N0", vi)} → {ticket.NetWeightKg?.ToString("N0", vi)} kg");
        if (before.Price != ticket.UnitPrice)
            changes.Add($"Đơn giá: {before.Price?.ToString("N0", vi) ?? "(chưa có)"} → {ticket.UnitPrice?.ToString("N0", vi)}");
        if (changes.Count == 0)
        {
            if (!wasAdjusted)
            {
                ticket.OriginalNetKg = ticket.OriginalBillableKg = ticket.OriginalUnitPrice = null;
                ticket.OriginalAmountVnd = null;
            }

            return OperationResult<TicketRecord>.Fail("Chưa có gì thay đổi so với số hiện tại.");
        }

        var when = now ?? DateTimeOffset.Now;
        ticket.AdjustedAt = ticket.IsAdjusted ? when : null;
        ticket.AdjustedBy = user;
        ticket.AdjustReason = reason.Trim();
        ticket.AdjustLog = (ticket.AdjustLog is { Length: > 0 } log ? log + "\n" : "") + $"{when:dd/MM/yyyy HH:mm} · {user}: {string.Join("; ", changes)} · {reason.Trim()}";
        ticket.UpdatedAt = when;
        await db.SaveChangesAsync(ct);
        return OperationResult<TicketRecord>.Success(ticket);
    }

    /// <summary>Goes back to exactly what the weighing app sent.</summary>
    public async Task<OperationResult<TicketRecord>> RestoreAsync(int ticketId, string user, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        var problem = await CheckEditableAsync(ticket, ct);
        if (problem is not null)
            return OperationResult<TicketRecord>.Fail(problem);
        if (!ticket!.IsAdjusted)
            return OperationResult<TicketRecord>.Fail("Phiếu này chưa bị chỉnh.");

        var when = now ?? DateTimeOffset.Now;
        var vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var was = ticket.NetWeightKg?.ToString("N0", vi);
        ticket.AdjustedNetKg = null;
        ticket.AdjustedUnitPrice = null;
        TicketPricing.Reapply(ticket);
        ticket.AdjustedAt = null;
        ticket.AdjustedBy = user;
        ticket.AdjustReason = null;
        ticket.AdjustLog = (ticket.AdjustLog is { Length: > 0 } log ? log + "\n" : "") + $"{when:dd/MM/yyyy HH:mm} · {user}: Khôi phục theo phiếu cân ({was} → {ticket.NetWeightKg?.ToString("N0", vi)} kg)";
        ticket.UpdatedAt = when;
        await db.SaveChangesAsync(ct);
        return OperationResult<TicketRecord>.Success(ticket);
    }

    private async Task<string?> CheckEditableAsync(TicketRecord? ticket, CancellationToken ct)
    {
        if (ticket is null || ticket.IsDeleted)
            return "Không tìm thấy phiếu.";
        if (ticket.AwaitingSecondWeigh)
            return "Phiếu đang chờ cân lần 2 nên chưa chỉnh được.";
        if (await db.Seasons.AnyAsync(s => s.IsClosed && s.StartDate <= ticket.TicketDate && (s.EndDate == null || s.EndDate >= ticket.TicketDate), ct))
            return "Mùa này đã chốt, không chỉnh được phiếu.";
        return null;
    }
}