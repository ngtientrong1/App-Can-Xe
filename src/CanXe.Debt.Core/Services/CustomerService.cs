using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

/// <summary>A customer name that came in on tickets but is not matched to a customer yet.</summary>
public sealed record UnmappedName(string Normalized, string DisplayName, int TicketCount, long TotalAmountVnd);

/// <summary>How much is recorded under a customer: live tickets, payments, lots and extra fees.</summary>
public sealed record CustomerFootprint(int Tickets, int Payments, int Lots, int Extras);

public sealed class CustomerService(DebtDbContext db)
{
    public Task<List<Customer>> GetAllAsync(CancellationToken ct = default) =>
        db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

    public Task<Customer?> GetAsync(int id, CancellationToken ct = default) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<OperationResult<Customer>> CreateAsync(string name, string? phone = null, string? note = null, CancellationToken ct = default)
    {
        name = name.Trim();
        var normalized = NameNormalizer.Normalize(name);
        if (normalized.Length == 0)
            return OperationResult<Customer>.Fail("Nhập tên chủ hàng.");
        if (await IsNameTakenAsync(normalized, null, ct))
            return OperationResult<Customer>.Fail("Tên này trùng với một chủ hàng đã có.");

        var customer = new Customer { Name = name, NormalizedName = normalized, Phone = Clean(phone), Note = Clean(note) };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        await RemapAsync(ct);
        return OperationResult<Customer>.Success(customer);
    }

    public async Task<OperationResult> UpdateAsync(int id, string name, string? phone, string? note, bool isActive, CancellationToken ct = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
            return OperationResult.Fail("Không tìm thấy chủ hàng.");

        name = name.Trim();
        var normalized = NameNormalizer.Normalize(name);
        if (normalized.Length == 0)
            return OperationResult.Fail("Nhập tên chủ hàng.");
        if (await IsNameTakenAsync(normalized, id, ct))
            return OperationResult.Fail("Tên này trùng với một chủ hàng khác.");

        if (normalized != customer.NormalizedName)
        {
            // The old spelling may still arrive on tickets from a scale station, so keep it as an alias.
            if (!await db.CustomerAliases.AnyAsync(a => a.NormalizedName == customer.NormalizedName, ct))
                db.CustomerAliases.Add(new CustomerAlias { CustomerId = id, RawName = customer.Name, NormalizedName = customer.NormalizedName });
            var staleAlias = await db.CustomerAliases.FirstOrDefaultAsync(a => a.NormalizedName == normalized, ct);
            if (staleAlias is not null)
                db.CustomerAliases.Remove(staleAlias);
        }

        customer.Name = name;
        customer.NormalizedName = normalized;
        customer.Phone = Clean(phone);
        customer.Note = Clean(note);
        customer.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }

    /// <summary>What deleting a customer would take with it, to show before asking.</summary>
    public async Task<CustomerFootprint> GetFootprintAsync(int id, CancellationToken ct = default) => new(
        await db.Tickets.CountAsync(t => t.CustomerId == id && !t.IsDeleted, ct),
        await db.Payments.CountAsync(p => p.CustomerId == id && p.VoidedAt == null, ct),
        await db.Periods.CountAsync(p => p.CustomerId == id, ct),
        await db.PeriodExtras.CountAsync(e => e.CustomerId == id && e.VoidedAt == null, ct));

    /// <summary>
    /// Removes a seller who is paid on the spot and needs no debt tracking: the customer, their lots, payments and extra fees,
    /// and their tickets leave the ledger (tickets are only marked deleted, and a backup keeps everything). Tickets of the same
    /// name that arrive in a later file show up on the name-matching page again, to keep or drop.
    /// </summary>
    public async Task<OperationResult<CustomerFootprint>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
            return OperationResult<CustomerFootprint>.Fail("Không tìm thấy chủ hàng.");

        var closed = await db.Seasons.AsNoTracking().Where(s => s.IsClosed).ToListAsync(ct);
        var tickets = await db.Tickets.Where(t => t.CustomerId == id).ToListAsync(ct);
        var payments = await db.Payments.Where(p => p.CustomerId == id).ToListAsync(ct);
        if (closed.Count > 0
            && (tickets.Any(t => !t.IsDeleted && closed.Any(s => s.Contains(t.TicketDate)))
                || payments.Any(p => p.VoidedAt == null && closed.Any(s => s.Id == p.SeasonId))))
            return OperationResult<CustomerFootprint>.Fail("Chủ hàng này có dữ liệu trong mùa đã chốt nên không xóa được.");

        var footprint = await GetFootprintAsync(id, ct);
        var now = DateTimeOffset.Now;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var ticket in tickets)
        {
            if (!ticket.IsDeleted)
            {
                ticket.IsDeleted = true;
                ticket.UpdatedAt = now;
            }

            ticket.CustomerId = null;
            ticket.PeriodId = null;
        }

        db.Payments.RemoveRange(payments);
        db.PeriodExtras.RemoveRange(await db.PeriodExtras.Where(e => e.CustomerId == id).ToListAsync(ct));
        db.Periods.RemoveRange(await db.Periods.Where(p => p.CustomerId == id).ToListAsync(ct));
        db.CustomerAliases.RemoveRange(await db.CustomerAliases.Where(a => a.CustomerId == id).ToListAsync(ct));
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OperationResult<CustomerFootprint>.Success(footprint);
    }

    /// <summary>Drops the tickets of a name that is not matched to any customer ("not tracked as a debt"); returns how many.</summary>
    public async Task<int> DismissNameAsync(string normalizedName, CancellationToken ct = default)
    {
        if (normalizedName.Length == 0)
            return 0;

        var tickets = await db.Tickets.Where(t => t.CustomerId == null && !t.IsDeleted && t.CustomerNameNormalized == normalizedName).ToListAsync(ct);
        var now = DateTimeOffset.Now;
        foreach (var ticket in tickets)
        {
            ticket.IsDeleted = true;
            ticket.PeriodId = null;
            ticket.UpdatedAt = now;
        }

        if (tickets.Count > 0)
            await db.SaveChangesAsync(ct);
        return tickets.Count;
    }

    public async Task<List<UnmappedName>> GetUnmappedNamesAsync(CancellationToken ct = default)
    {
        var tickets = await db.Tickets.AsNoTracking()
            .Where(t => t.CustomerId == null && !t.IsDeleted)
            .Select(t => new { t.CustomerNameNormalized, t.CustomerNameRaw, t.AmountVnd })
            .ToListAsync(ct);

        return tickets
            .GroupBy(t => t.CustomerNameNormalized ?? "")
            .Select(g => new UnmappedName(
                g.Key,
                g.Select(t => t.CustomerNameRaw).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "(không có tên chủ hàng)",
                g.Count(),
                g.Sum(t => t.AmountVnd ?? 0)))
            .OrderByDescending(n => n.TicketCount)
            .ToList();
    }

    /// <summary>Tells the app that an unmatched name means an existing customer, from now on and for past tickets.</summary>
    public async Task<OperationResult> MapNameToCustomerAsync(string normalizedName, string rawName, int customerId, CancellationToken ct = default)
    {
        if (normalizedName.Length == 0)
            return OperationResult.Fail("Phiếu không có tên chủ hàng nên không ghép được. Hãy sửa tên chủ hàng ở app cân rồi xuất lại file.");
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            return OperationResult.Fail("Không tìm thấy chủ hàng.");

        if (!await db.CustomerAliases.AnyAsync(a => a.NormalizedName == normalizedName, ct)
            && !await db.Customers.AnyAsync(c => c.NormalizedName == normalizedName, ct))
        {
            db.CustomerAliases.Add(new CustomerAlias { CustomerId = customerId, RawName = rawName, NormalizedName = normalizedName });
            await db.SaveChangesAsync(ct);
        }

        await RemapAsync(ct);
        return OperationResult.Success;
    }

    public async Task<OperationResult<Customer>> CreateFromNameAsync(string rawName, CancellationToken ct = default) =>
        await CreateAsync(rawName, ct: ct);

    /// <summary>Resolves customers for every ticket that has none, using names and aliases.</summary>
    public async Task<int> RemapAsync(CancellationToken ct = default)
    {
        var lookup = await BuildLookupAsync(ct);
        var unmapped = await db.Tickets.Where(t => t.CustomerId == null && t.CustomerNameNormalized != null).ToListAsync(ct);
        var changed = 0;
        foreach (var ticket in unmapped)
        {
            if (lookup.TryGetValue(ticket.CustomerNameNormalized!, out var customerId))
            {
                ticket.CustomerId = customerId;
                changed++;
            }
        }

        if (changed > 0)
            await db.SaveChangesAsync(ct);
        return changed;
    }

    public async Task<Dictionary<string, int>> BuildLookupAsync(CancellationToken ct = default)
    {
        var lookup = new Dictionary<string, int>();
        foreach (var alias in await db.CustomerAliases.AsNoTracking().ToListAsync(ct))
            lookup[alias.NormalizedName] = alias.CustomerId;
        foreach (var customer in await db.Customers.AsNoTracking().ToListAsync(ct))
            lookup[customer.NormalizedName] = customer.Id;
        return lookup;
    }

    private async Task<bool> IsNameTakenAsync(string normalized, int? exceptId, CancellationToken ct) =>
        await db.Customers.AnyAsync(c => c.NormalizedName == normalized && c.Id != exceptId, ct);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
