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
            t => t.TicketDateTime,
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

    public async Task<VehicleUsageContext?> GetVehicleUsageContextAsync(
        string normalizedPlate,
        CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.Normalize(normalizedPlate);
        if (normalized.Length == 0)
            return null;

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.NormalizedPlateNumber == normalized, cancellationToken);

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

        var frequent = FrequentCargoTypeResolver.Resolve(
            tickets.Select(t => new CargoUsageTicketRow(
                t.CargoTypeId,
                t.CargoTypeNameSnapshot,
                t.TicketDateTime)).ToList());

        var recentWithCustomer = tickets.FirstOrDefault(t =>
            t.CustomerId.HasValue || !string.IsNullOrWhiteSpace(t.CustomerNameSnapshot));

        var recentCustomerId = recentWithCustomer?.CustomerId;
        var recentCustomerName = recentWithCustomer?.CustomerNameSnapshot;

        if (string.IsNullOrWhiteSpace(recentCustomerName) && vehicle.LastCustomerId.HasValue)
        {
            var lastCustomer = await _db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == vehicle.LastCustomerId.Value, cancellationToken);
            if (lastCustomer is not null)
            {
                recentCustomerId = lastCustomer.Id;
                recentCustomerName = lastCustomer.Name;
            }
        }

        var recentWithCargo = tickets.FirstOrDefault(t =>
            t.CargoTypeId.HasValue || !string.IsNullOrWhiteSpace(t.CargoTypeNameSnapshot));

        return new VehicleUsageContext
        {
            VehicleId = vehicle.Id,
            PlateNumber = vehicle.PlateNumber,
            RecentCustomerId = recentCustomerId,
            RecentCustomerName = recentCustomerName,
            RecentCargoTypeId = recentWithCargo?.CargoTypeId,
            RecentCargoTypeName = recentWithCargo?.CargoTypeNameSnapshot,
            FrequentCargoTypeId = frequent?.CargoTypeId,
            FrequentCargoTypeName = frequent?.CargoTypeName,
            FrequentCargoUsageCount = frequent?.UsageCount ?? 0,
            LastUsedAt = tickets.Max(t => t.TicketDateTime)
        };
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
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .Take(maxResults)
            .ToList();

        return (await _db.Customers
            .Where(c => c.IsActive && c.NormalizedName.Contains(normalized))
            .ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .Take(maxResults)
            .ToList();
    }

    public Task<Customer?> FindByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.NormalizedName == normalizedName, cancellationToken);

    public async Task<Customer> UpsertAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        var normalized = TextNormalizer.Normalize(trimmed);
        var existing = await FindByNormalizedNameAsync(normalized, cancellationToken);

        if (existing is not null)
        {
            existing.LastUsedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var customer = new Customer
        {
            Name = trimmed,
            NormalizedName = normalized,
            LastUsedAt = DateTimeOffset.Now,
            IsActive = true
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
            .Where(c => c.IsActive && c.NormalizedName.Contains(prefix))
            .ToListAsync(cancellationToken))
            .OrderByDescending(c => c.LastUsedAt)
            .Take(maxResults)
            .ToList();
    }
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
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Take(maxResults)
                .ToListAsync(cancellationToken);

        return await _db.CargoTypes
            .Where(c => c.IsActive && c.NormalizedName.Contains(normalized))
            .OrderBy(c => c.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }

    public Task<CargoType?> FindByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        _db.CargoTypes.FirstOrDefaultAsync(c => c.NormalizedName == normalizedName, cancellationToken);

    public async Task<CargoType> UpsertAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        var normalized = TextNormalizer.Normalize(trimmed);
        var existing = await FindByNormalizedNameAsync(normalized, cancellationToken);
        if (existing is not null)
            return existing;

        var cargoType = new CargoType
        {
            Name = trimmed,
            NormalizedName = normalized,
            IsActive = true
        };
        _db.CargoTypes.Add(cargoType);
        await _db.SaveChangesAsync(cancellationToken);
        return cargoType;
    }
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

    public async Task<IReadOnlyList<Vehicle>> SearchAsync(
        string searchTerm,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var normalized = PlateNormalizer.Normalize(searchTerm);
        if (normalized.Length == 0)
            return (await _db.Vehicles.Include(v => v.LastCustomer).ToListAsync(cancellationToken))
                .OrderByDescending(v => v.LastUsedAt)
                .Take(maxResults)
                .ToList();

        return (await _db.Vehicles
            .Include(v => v.LastCustomer)
            .Where(v => v.NormalizedPlateNumber.Contains(normalized))
            .ToListAsync(cancellationToken))
            .OrderByDescending(v => v.LastUsedAt)
            .Take(maxResults)
            .ToList();
    }

    public async Task<VehicleSuggestion?> GetSuggestionForPlateAsync(
        string plateNumber,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles
            .Include(v => v.LastCustomer)
            .FirstOrDefaultAsync(v => v.NormalizedPlateNumber == PlateNormalizer.Normalize(plateNumber), cancellationToken);

        if (vehicle is null)
            return null;

        return new VehicleSuggestion
        {
            PlateNumber = vehicle.PlateNumber,
            LastCustomerName = vehicle.LastCustomer?.Name
        };
    }

    public async Task<Vehicle> UpsertAsync(
        string plateNumber,
        int? customerId,
        CancellationToken cancellationToken = default)
    {
        var trimmed = plateNumber.Trim().ToUpperInvariant();
        var normalized = PlateNormalizer.Normalize(trimmed);
        var existing = await FindByPlateAsync(trimmed, cancellationToken);

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
            LastUsedAt = DateTimeOffset.Now
        };
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(cancellationToken);
        return vehicle;
    }
}
