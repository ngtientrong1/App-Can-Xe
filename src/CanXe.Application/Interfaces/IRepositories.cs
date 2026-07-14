using CanXe.Application.Models;
using CanXe.Domain.Entities;

namespace CanXe.Application.Interfaces;

public interface IWeighTicketRepository
{
    Task<WeighTicket?> GetByIdWithEventsAsync(int id, CancellationToken cancellationToken = default);
    Task<WeighTicket?> GetByIdIncludingDeletedAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeighTicket>> GetFilteredAsync(WeighTicketFilter filter, CancellationToken cancellationToken = default);
    Task<WeighTicket> AddAsync(WeighTicket ticket, CancellationToken cancellationToken = default);
    Task UpdateAsync(WeighTicket ticket, CancellationToken cancellationToken = default);
    Task<WeighEvent> AddEventAsync(WeighEvent weighEvent, CancellationToken cancellationToken = default);
    Task<int> GetNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<int> PeekNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<string?> GetLatestPlateForCustomerAsync(string customerName, CancellationToken cancellationToken = default);
    Task<int> GetCargoUsageCountAsync(string cargoTypeName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchRecentNotesAsync(string searchTerm, int maxResults = 5, CancellationToken cancellationToken = default);
    Task<VehicleUsageContext?> GetVehicleUsageContextAsync(string normalizedPlate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRecentCustomerNamesAsync(int maxResults = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRecentLicensePlatesAsync(int maxResults = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRecentCargoTypeNamesAsync(int maxResults = 50, CancellationToken cancellationToken = default);
    Task<decimal?> GetRecentUnitPriceForCargoAsync(string cargoTypeName, CancellationToken cancellationToken = default);
    Task UpdateTicketEditAsync(
        WeighTicket ticket,
        IReadOnlyList<WeighEvent> events,
        IReadOnlyList<AuditLog> auditLogs,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetAuditLogsForTicketAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> SearchAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<Customer?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task<Customer?> FindActiveByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task<Customer> UpsertAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> FindSimilarAsync(string name, int maxResults = 5, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> ListCatalogAsync(string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListActiveNamesForHistoryAsync(int maxResults = 50, CancellationToken cancellationToken = default);
    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Customer> SaveCatalogAsync(Customer customer, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountTicketUsageAsync(int customerId, CancellationToken cancellationToken = default);
}

public interface ICargoTypeRepository
{
    Task<IReadOnlyList<CargoType>> SearchAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<CargoType?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task<CargoType?> FindActiveByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task<CargoType> UpsertAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CargoType>> ListCatalogAsync(string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListActiveNamesForHistoryAsync(int maxResults = 50, CancellationToken cancellationToken = default);
    Task<CargoType?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CargoType> SaveCatalogAsync(CargoType cargoType, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountTicketUsageAsync(int cargoTypeId, CancellationToken cancellationToken = default);
}

public interface IVehicleRepository
{
    Task<Vehicle?> FindByPlateAsync(string plateNumber, CancellationToken cancellationToken = default);
    Task<Vehicle?> FindActiveByPlateAsync(string plateNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> SearchAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<Vehicle> UpsertAsync(string plateNumber, int? customerId, CancellationToken cancellationToken = default);
    Task<VehicleSuggestion?> GetSuggestionForPlateAsync(string plateNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> ListCatalogAsync(string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListActivePlatesForHistoryAsync(int maxResults = 50, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Vehicle> SaveCatalogAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountTicketUsageAsync(int vehicleId, CancellationToken cancellationToken = default);
}
