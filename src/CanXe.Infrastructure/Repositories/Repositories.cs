using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Infrastructure.Repositories;

public sealed class WeighTicketRepository : IWeighTicketRepository
{
    private readonly CanXeDbContext _db;

    public WeighTicketRepository(CanXeDbContext db) => _db = db;

    public async Task<WeighTicket?> GetByIdWithEventsAsync(int id, CancellationToken cancellationToken = default) =>
        await _db.WeighTickets
            .Include(t => t.Events)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);

    public async Task<WeighTicket?> GetByIdIncludingDeletedAsync(int id, CancellationToken cancellationToken = default) =>
        await _db.WeighTickets
            .Include(t => t.Events)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WeighTicket>> GetFilteredAsync(
        WeighTicketFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _db.WeighTickets
            .Include(t => t.Events)
            .Where(t => !t.IsDeleted)
            .AsQueryable();

        var hasDateFrom = filter.FromDate is not null;
        var hasDateTo = filter.ToDate is not null;
        var fromDate = filter.FromDate;
        var toDate = filter.ToDate;

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            var term = filter.CustomerName.Trim();
            query = query.Where(t => t.CustomerNameSnapshot != null &&
                t.CustomerNameSnapshot.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.CargoTypeName))
        {
            var term = filter.CargoTypeName.Trim();
            query = query.Where(t => t.CargoTypeNameSnapshot != null &&
                t.CargoTypeNameSnapshot.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.LicensePlate))
        {
            var term = filter.LicensePlate.Trim();
            query = query.Where(t => t.LicensePlateSnapshot != null &&
                t.LicensePlateSnapshot.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.DisplayNumber))
        {
            var term = filter.DisplayNumber.Trim();
            query = query.Where(t => t.DisplayNumber.Contains(term));
        }

        if (filter.UnitPriceVndPerKg is { } price)
        {
            var vnd = (int)Math.Round(price, 0, MidpointRounding.AwayFromZero);
            query = query.Where(t => t.UnitPriceVndPerKg == vnd);
        }

        var results = await query.ToListAsync(cancellationToken);

        if (hasDateFrom)
            results = results.Where(t => t.TicketDateTime >= fromDate!.Value).ToList();

        if (hasDateTo)
            results = results.Where(t => t.TicketDateTime <= toDate!.Value).ToList();

        if (filter.UnitPriceFromVndPerKg is { } fromUnitPrice)
        {
            var vndFrom = (int)Math.Round(fromUnitPrice, 0, MidpointRounding.AwayFromZero);
            results = results.Where(t => t.UnitPriceVndPerKg >= vndFrom).ToList();
        }

        if (filter.UnitPriceToVndPerKg is { } toUnitPrice)
        {
            var vndTo = (int)Math.Round(toUnitPrice, 0, MidpointRounding.AwayFromZero);
            results = results.Where(t => t.UnitPriceVndPerKg <= vndTo).ToList();
        }

        return TicketListSorter.SortNewestFirst(
            results,
            t => t.CreatedAt != default ? t.CreatedAt : t.TicketDateTime,
            t => t.SequenceNumber,
            t => t.Id)
            .Take(filter.MaxResults > 0 ? filter.MaxResults : 200)
            .ToList();
    }

    public async Task<WeighTicket> AddAsync(WeighTicket ticket, CancellationToken cancellationToken = default)
    {
        _db.WeighTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task UpdateAsync(WeighTicket ticket, CancellationToken cancellationToken = default)
    {
        _db.WeighTickets.Update(ticket);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WeighEvent> AddEventAsync(WeighEvent weighEvent, CancellationToken cancellationToken = default)
    {
        _db.WeighEvents.Add(weighEvent);
        await _db.SaveChangesAsync(cancellationToken);
        return weighEvent;
    }

    public async Task<int> GetNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var row = await _db.TicketSequences
            .FirstOrDefaultAsync(s => s.Year == year && s.Month == month, cancellationToken);

        if (row is null)
        {
            row = new TicketSequence { Year = year, Month = month, LastSequence = 1 };
            _db.TicketSequences.Add(row);
        }
        else
        {
            row.LastSequence += 1;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return row.LastSequence;
    }

    public async Task<int> PeekNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var row = await _db.TicketSequences
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Year == year && s.Month == month, cancellationToken);

        return row is null ? 1 : row.LastSequence + 1;
    }

    public async Task<string?> GetLatestPlateForCustomerAsync(
        string customerName,
        CancellationToken cancellationToken = default)
    {
        var term = customerName.Trim();
        var tickets = await _db.WeighTickets
            .Where(t => !t.IsDeleted && t.CustomerNameSnapshot != null && t.CustomerNameSnapshot == term && t.LicensePlateSnapshot != null)
            .ToListAsync(cancellationToken);

        return tickets
            .OrderByDescending(t => t.TicketDateTime)
            .Select(t => t.LicensePlateSnapshot)
            .FirstOrDefault();
    }

    public async Task<int> GetCargoUsageCountAsync(
        string cargoTypeName,
        CancellationToken cancellationToken = default)
    {
        var term = cargoTypeName.Trim();
        return await _db.WeighTickets
            .CountAsync(t => !t.IsDeleted && t.CargoTypeNameSnapshot == term, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SearchRecentNotesAsync(
        string searchTerm,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var notes = await _db.WeighTickets
            .Where(t => !t.IsDeleted && t.Notes != null && t.Notes != string.Empty)
            .Select(t => t.Notes!)
            .ToListAsync(cancellationToken);

        var distinct = notes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(n => TextNormalizer.ContainsNormalized(n, searchTerm))
            .Take(maxResults)
            .ToList();

        return distinct;
    }

    public async Task<IReadOnlyList<string>> GetRecentCustomerNamesAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default) =>
        await GetRecentDistinctFieldValuesAsync(
            t => t.CustomerNameSnapshot,
            maxResults,
            cancellationToken);

    public async Task<IReadOnlyList<string>> GetRecentLicensePlatesAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default) =>
        await GetRecentDistinctFieldValuesAsync(
            t => t.LicensePlateSnapshot,
            maxResults,
            cancellationToken);

    public async Task<IReadOnlyList<string>> GetRecentCargoTypeNamesAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default) =>
        await GetRecentDistinctFieldValuesAsync(
            t => t.CargoTypeNameSnapshot,
            maxResults,
            cancellationToken);

    public async Task<decimal?> GetRecentUnitPriceForCargoAsync(
        string cargoTypeName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cargoTypeName))
            return null;

        var term = cargoTypeName.Trim();
        var tickets = await _db.WeighTickets
            .Where(t => !t.IsDeleted &&
                        t.CargoTypeNameSnapshot == term &&
                        t.UnitPriceVndPerKg != null &&
                        t.UnitPriceVndPerKg > 0)
            .ToListAsync(cancellationToken);

        var latest = tickets
            .OrderByDescending(t => t.TicketDateTime)
            .FirstOrDefault();

        return latest?.UnitPriceVndPerKg;
    }

    private async Task<IReadOnlyList<string>> GetRecentDistinctFieldValuesAsync(
        Func<WeighTicket, string?> selector,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var tickets = await _db.WeighTickets
            .Where(t => !t.IsDeleted)
            .ToListAsync(cancellationToken);

        return tickets
            .Select(t => new { Value = selector(t), t.TicketDateTime })
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .GroupBy(x => x.Value!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Value = g.First().Value!.Trim(),
                LastUsed = g.Max(x => x.TicketDateTime)
            })
            .OrderByDescending(x => x.LastUsed)
            .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(x => x.Value)
            .ToList();
    }

    public async Task<VehicleUsageContext?> GetVehicleUsageContextAsync(
        string normalizedPlate,
        CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.Normalize(normalizedPlate);
        if (normalized.Length == 0)
            return null;

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.NormalizedPlateNumber == normalized && v.IsActive && !v.IsDeleted, cancellationToken);

        if (vehicle is null)
            return null;

        var tickets = (await _db.WeighTickets
            .Where(t => !t.IsDeleted && (t.VehicleId == vehicle.Id ||
                        (t.LicensePlateSnapshot != null && t.LicensePlateSnapshot == vehicle.PlateNumber)))
            .ToListAsync(cancellationToken))
            .OrderByDescending(t => t.TicketDateTime)
            .Take(FrequentCargoTypeResolver.MaxTicketsToAnalyze)
            .ToList();

        if (tickets.Count == 0)
            return null;

        var activeCargoTickets = new List<CargoUsageTicketRow>();
        foreach (var ticket in tickets)
        {
            if (await IsActiveCargoReferenceAsync(ticket.CargoTypeId, ticket.CargoTypeNameSnapshot, cancellationToken))
            {
                activeCargoTickets.Add(new CargoUsageTicketRow(
                    ticket.CargoTypeId,
                    ticket.CargoTypeNameSnapshot,
                    ticket.TicketDateTime));
            }
        }

        var recentWithCustomer = tickets.FirstOrDefault(t =>
            t.CustomerId.HasValue || !string.IsNullOrWhiteSpace(t.CustomerNameSnapshot));

        int? recentCustomerId = null;
        string? recentCustomerName = null;
        if (recentWithCustomer is not null &&
            await IsActiveCustomerReferenceAsync(
                recentWithCustomer.CustomerId,
                recentWithCustomer.CustomerNameSnapshot,
                cancellationToken))
        {
            recentCustomerId = recentWithCustomer.CustomerId;
            recentCustomerName = recentWithCustomer.CustomerNameSnapshot;
        }

        if (string.IsNullOrWhiteSpace(recentCustomerName) && vehicle.LastCustomerId.HasValue)
        {
            var lastCustomer = await _db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == vehicle.LastCustomerId.Value && c.IsActive && !c.IsDeleted, cancellationToken);
            if (lastCustomer is not null)
            {
                recentCustomerId = lastCustomer.Id;
                recentCustomerName = lastCustomer.Name;
            }
        }

        var recentWithCargo = tickets.FirstOrDefault(t =>
            t.CargoTypeId.HasValue || !string.IsNullOrWhiteSpace(t.CargoTypeNameSnapshot));

        int? recentCargoTypeId = null;
        string? recentCargoTypeName = null;
        if (recentWithCargo is not null &&
            await IsActiveCargoReferenceAsync(
                recentWithCargo.CargoTypeId,
                recentWithCargo.CargoTypeNameSnapshot,
                cancellationToken))
        {
            recentCargoTypeId = recentWithCargo.CargoTypeId;
            recentCargoTypeName = recentWithCargo.CargoTypeNameSnapshot;
        }

        var frequentResult = FrequentCargoTypeResolver.Resolve(activeCargoTickets);
        int? frequentCargoTypeId = frequentResult?.CargoTypeId;
        string? frequentCargoTypeName = frequentResult?.CargoTypeName;
        var frequentUsageCount = frequentResult?.UsageCount ?? 0;

        if (frequentCargoTypeName is not null &&
            !await IsActiveCargoReferenceAsync(frequentCargoTypeId, frequentCargoTypeName, cancellationToken))
        {
            frequentCargoTypeId = null;
            frequentCargoTypeName = null;
            frequentUsageCount = 0;
        }

        return new VehicleUsageContext
        {
            VehicleId = vehicle.Id,
            PlateNumber = vehicle.PlateNumber,
            RecentCustomerId = recentCustomerId,
            RecentCustomerName = recentCustomerName,
            RecentCargoTypeId = recentCargoTypeId,
            RecentCargoTypeName = recentCargoTypeName,
            FrequentCargoTypeId = frequentCargoTypeId,
            FrequentCargoTypeName = frequentCargoTypeName,
            FrequentCargoUsageCount = frequentUsageCount,
            LastUsedAt = tickets.Max(t => t.TicketDateTime)
        };
    }

    private async Task<bool> IsActiveCustomerReferenceAsync(
        int? customerId,
        string? customerName,
        CancellationToken cancellationToken)
    {
        if (customerId.HasValue)
        {
            return await _db.Customers.AsNoTracking()
                .AnyAsync(c => c.Id == customerId.Value && c.IsActive && !c.IsDeleted, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(customerName))
            return false;

        var normalized = TextNormalizer.Normalize(customerName);
        return await _db.Customers.AsNoTracking()
            .AnyAsync(c => c.NormalizedName == normalized && c.IsActive && !c.IsDeleted, cancellationToken);
    }

    private async Task<bool> IsActiveCargoReferenceAsync(
        int? cargoTypeId,
        string? cargoTypeName,
        CancellationToken cancellationToken)
    {
        if (cargoTypeId.HasValue)
        {
            return await _db.CargoTypes.AsNoTracking()
                .AnyAsync(c => c.Id == cargoTypeId.Value && c.IsActive && !c.IsDeleted, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(cargoTypeName))
            return false;

        var normalized = TextNormalizer.Normalize(cargoTypeName);
        return await _db.CargoTypes.AsNoTracking()
            .AnyAsync(c => c.NormalizedName == normalized && c.IsActive && !c.IsDeleted, cancellationToken);
    }

    public async Task UpdateTicketEditAsync(
        WeighTicket ticket,
        IReadOnlyList<WeighEvent> events,
        IReadOnlyList<AuditLog> auditLogs,
        CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            _db.WeighTickets.Update(ticket);

            var existingInDb = await _db.WeighEvents
                .Where(e => e.WeighTicketId == ticket.Id)
                .ToListAsync(cancellationToken);

            var incomingIds = events.Where(e => e.Id > 0).Select(e => e.Id).ToHashSet();
            var toRemove = existingInDb.Where(e => !incomingIds.Contains(e.Id)).ToList();
            if (toRemove.Count > 0)
                _db.WeighEvents.RemoveRange(toRemove);

            foreach (var weighEvent in events)
            {
                if (weighEvent.Id == 0)
                    await _db.WeighEvents.AddAsync(weighEvent, cancellationToken);
                else
                    _db.WeighEvents.Update(weighEvent);
            }

            if (auditLogs.Count > 0)
                await _db.AuditLogs.AddRangeAsync(auditLogs, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetAuditLogsForTicketAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var logs = await _db.AuditLogs
            .Where(a => a.TicketId == ticketId)
            .ToListAsync(cancellationToken);

        return logs.OrderByDescending(a => a.EditedAt.UtcDateTime).ToList();
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly CanXeDbContext _db;

    public CustomerRepository(CanXeDbContext db) => _db = db;

    public async Task<IReadOnlyList<Customer>> SearchAsync(
        string searchTerm,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var normalized = TextNormalizer.Normalize(searchTerm);
        if (normalized.Length == 0)
            return (await _db.Customers
                .Where(c => c.IsActive && !c.IsDeleted)
                .ToListAsync(cancellationToken))
                .OrderByDescending(c => c.LastUsedAt)
                .Take(maxResults)
                .ToList();

        return (await _db.Customers
            .Where(c => c.IsActive && !c.IsDeleted && c.NormalizedName.Contains(normalized))
            .ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .Take(maxResults)
            .ToList();
    }

    public Task<Customer?> FindByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.NormalizedName == normalizedName, cancellationToken);

    public Task<Customer?> FindActiveByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        _db.Customers.FirstOrDefaultAsync(
            c => c.NormalizedName == normalizedName && c.IsActive && !c.IsDeleted,
            cancellationToken);

    public async Task<Customer> UpsertAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name is required.", nameof(name));

        var trimmed = TextNormalizer.CollapseSpaces(name.Trim());
        var normalized = TextNormalizer.Normalize(trimmed);
        if (normalized.Length == 0)
            throw new ArgumentException("Customer name is required after normalization.", nameof(name));

        var existing = await FindActiveByNormalizedNameAsync(normalized, cancellationToken);

        if (existing is not null)
        {
            existing.LastUsedAt = DateTimeOffset.Now;
            existing.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var now = DateTimeOffset.Now;
        var customer = new Customer
        {
            Name = trimmed,
            NormalizedName = normalized,
            Phone = null,
            Address = null,
            Note = null,
            LastUsedAt = now,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = null
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<IReadOnlyList<Customer>> FindSimilarAsync(
        string name,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var normalized = TextNormalizer.Normalize(name);
        if (normalized.Length == 0)
            return [];

        var prefixLength = Math.Min(3, normalized.Length);
        var prefix = normalized[..prefixLength];

        return (await _db.Customers
            .Where(c => c.IsActive && !c.IsDeleted && c.NormalizedName.Contains(prefix))
            .ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .Take(maxResults)
            .ToList();
    }

    public async Task<IReadOnlyList<Customer>> ListCatalogAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Customers.Where(c => !c.IsDeleted).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = TextNormalizer.Normalize(search);
            query = query.Where(c => c.NormalizedName.Contains(normalized));
        }

        return (await query.ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListActiveNamesForHistoryAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default) =>
        (await _db.Customers
            .Where(c => c.IsActive && !c.IsDeleted)
            .Select(c => new { c.Name, c.LastUsedAt })
            .ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .ThenBy(c => c.Name)
            .Take(maxResults)
            .Select(c => c.Name)
            .ToList();

    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);

    public async Task<Customer> SaveCatalogAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        customer.UpdatedAt = DateTimeOffset.Now;
        if (customer.Id == 0)
        {
            customer.CreatedAt = DateTimeOffset.Now;
            _db.Customers.Add(customer);
        }
        else
        {
            _db.Customers.Update(customer);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.IsActive = false;
        row.DeletedAt = DateTimeOffset.Now;
        row.UpdatedAt = DateTimeOffset.Now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountTicketUsageAsync(int customerId, CancellationToken cancellationToken = default) =>
        _db.WeighTickets.CountAsync(t => !t.IsDeleted && t.CustomerId == customerId, cancellationToken);
}

public sealed class CargoTypeRepository : ICargoTypeRepository
{
    private readonly CanXeDbContext _db;

    public CargoTypeRepository(CanXeDbContext db) => _db = db;

    public async Task<IReadOnlyList<CargoType>> SearchAsync(
        string searchTerm,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var normalized = TextNormalizer.Normalize(searchTerm);
        if (normalized.Length == 0)
            return await _db.CargoTypes
                .Where(c => c.IsActive && !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Take(maxResults)
                .ToListAsync(cancellationToken);

        return await _db.CargoTypes
            .Where(c => c.IsActive && !c.IsDeleted && c.NormalizedName.Contains(normalized))
            .OrderBy(c => c.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }

    public Task<CargoType?> FindByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        _db.CargoTypes.FirstOrDefaultAsync(c => c.NormalizedName == normalizedName, cancellationToken);

    public Task<CargoType?> FindActiveByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        _db.CargoTypes.FirstOrDefaultAsync(
            c => c.NormalizedName == normalizedName && c.IsActive && !c.IsDeleted,
            cancellationToken);

    public async Task<CargoType> UpsertAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = TextNormalizer.CollapseSpaces(name.Trim());
        var normalized = TextNormalizer.Normalize(trimmed);
        var existing = await FindActiveByNormalizedNameAsync(normalized, cancellationToken);
        if (existing is not null)
        {
            existing.LastUsedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var cargoType = new CargoType
        {
            Name = trimmed,
            NormalizedName = normalized,
            IsActive = true,
            Unit = "kg",
            LastUsedAt = DateTimeOffset.Now,
            CreatedAt = DateTimeOffset.Now
        };
        _db.CargoTypes.Add(cargoType);
        await _db.SaveChangesAsync(cancellationToken);
        return cargoType;
    }

    public async Task<IReadOnlyList<CargoType>> ListCatalogAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.CargoTypes.Where(c => !c.IsDeleted).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = TextNormalizer.Normalize(search);
            query = query.Where(c => c.NormalizedName.Contains(normalized));
        }

        return (await query.ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListActiveNamesForHistoryAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default) =>
        (await _db.CargoTypes
            .Where(c => c.IsActive && !c.IsDeleted)
            .Select(c => new { c.Name, c.LastUsedAt })
            .ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .ThenBy(c => c.Name)
            .Take(maxResults)
            .Select(c => c.Name)
            .ToList();

    public Task<CargoType?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.CargoTypes.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);

    public async Task<CargoType> SaveCatalogAsync(CargoType cargoType, CancellationToken cancellationToken = default)
    {
        cargoType.UpdatedAt = DateTimeOffset.Now;
        if (cargoType.Id == 0)
        {
            cargoType.CreatedAt = DateTimeOffset.Now;
            _db.CargoTypes.Add(cargoType);
        }
        else
        {
            _db.CargoTypes.Update(cargoType);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return cargoType;
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _db.CargoTypes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.IsActive = false;
        row.DeletedAt = DateTimeOffset.Now;
        row.UpdatedAt = DateTimeOffset.Now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountTicketUsageAsync(int cargoTypeId, CancellationToken cancellationToken = default) =>
        _db.WeighTickets.CountAsync(t => !t.IsDeleted && t.CargoTypeId == cargoTypeId, cancellationToken);
}

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly CanXeDbContext _db;

    public VehicleRepository(CanXeDbContext db) => _db = db;

    public async Task<Vehicle?> FindByPlateAsync(
        string plateNumber,
        CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.Normalize(plateNumber);
        return await _db.Vehicles
            .FirstOrDefaultAsync(v => v.NormalizedPlateNumber == normalized, cancellationToken);
    }

    public Task<Vehicle?> FindActiveByPlateAsync(
        string plateNumber,
        CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.Normalize(plateNumber);
        return _db.Vehicles.FirstOrDefaultAsync(
            v => v.NormalizedPlateNumber == normalized && v.IsActive && !v.IsDeleted,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Vehicle>> SearchAsync(
        string searchTerm,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.Normalize(searchTerm);
        if (normalized.Length == 0)
            return (await _db.Vehicles
                .Include(v => v.LastCustomer)
                .Where(v => v.IsActive && !v.IsDeleted)
                .ToListAsync(cancellationToken))
                .OrderByDescending(v => v.LastUsedAt)
                .Take(maxResults)
                .ToList();

        return (await _db.Vehicles
            .Include(v => v.LastCustomer)
            .Where(v => v.IsActive && !v.IsDeleted && v.NormalizedPlateNumber.Contains(normalized))
            .ToListAsync(cancellationToken))
            .OrderByDescending(v => v.LastUsedAt)
            .Take(maxResults)
            .ToList();
    }

    public async Task<VehicleSuggestion?> GetSuggestionForPlateAsync(
        string plateNumber,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await FindActiveByPlateAsync(plateNumber, cancellationToken);
        if (vehicle is null)
            return null;

        vehicle = await _db.Vehicles
            .Include(v => v.LastCustomer)
            .FirstOrDefaultAsync(v => v.Id == vehicle.Id, cancellationToken);

        return new VehicleSuggestion
        {
            PlateNumber = vehicle!.PlateNumber,
            LastCustomerName = vehicle.LastCustomer?.Name
        };
    }

    public async Task<Vehicle> UpsertAsync(
        string plateNumber,
        int? customerId,
        CancellationToken cancellationToken = default)
    {
        var trimmed = PlateNormalizer.FormatDisplay(plateNumber);
        var normalized = PlateNormalizer.Normalize(trimmed);
        var existing = await FindActiveByPlateAsync(trimmed, cancellationToken);

        if (existing is not null)
        {
            existing.LastUsedAt = DateTimeOffset.Now;
            existing.LastCustomerId = customerId;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var vehicle = new Vehicle
        {
            PlateNumber = trimmed,
            NormalizedPlateNumber = normalized,
            LastCustomerId = customerId,
            LastUsedAt = DateTimeOffset.Now,
            IsActive = true,
            CreatedAt = DateTimeOffset.Now
        };
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(cancellationToken);
        return vehicle;
    }

    public async Task<IReadOnlyList<Vehicle>> ListCatalogAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Vehicles.Include(v => v.LastCustomer).Where(v => !v.IsDeleted).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = PlateNormalizer.Normalize(search);
            query = query.Where(v => v.NormalizedPlateNumber.Contains(normalized));
        }

        return (await query.ToListAsync(cancellationToken))
            .OrderByDescending(v => v.LastUsedAt)
            .ThenBy(v => v.PlateNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListActivePlatesForHistoryAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default) =>
        (await _db.Vehicles
            .Where(v => v.IsActive && !v.IsDeleted)
            .Select(v => new { v.PlateNumber, v.LastUsedAt })
            .ToListAsync(cancellationToken))
            .OrderByDescending(v => v.LastUsedAt)
            .ThenBy(v => v.PlateNumber)
            .Take(maxResults)
            .Select(v => v.PlateNumber)
            .ToList();

    public Task<Vehicle?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Vehicles.Include(v => v.LastCustomer)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);

    public async Task<Vehicle> SaveCatalogAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        vehicle.UpdatedAt = DateTimeOffset.Now;
        if (vehicle.Id == 0)
        {
            vehicle.CreatedAt = DateTimeOffset.Now;
            _db.Vehicles.Add(vehicle);
        }
        else
        {
            _db.Vehicles.Update(vehicle);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return vehicle;
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (row is null)
            return;

        row.IsDeleted = true;
        row.IsActive = false;
        row.DeletedAt = DateTimeOffset.Now;
        row.UpdatedAt = DateTimeOffset.Now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountTicketUsageAsync(int vehicleId, CancellationToken cancellationToken = default) =>
        _db.WeighTickets.CountAsync(t => !t.IsDeleted && t.VehicleId == vehicleId, cancellationToken);
}
