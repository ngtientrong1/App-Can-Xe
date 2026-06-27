using CanXe.Application.Models;
using CanXe.Domain.Entities;

namespace CanXe.Application.Interfaces;

public interface IWeighTicketRepository
{
    Task<WeighTicket?> GetByIdWithEventsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeighTicket>> GetFilteredAsync(WeighTicketFilter filter, CancellationToken cancellationToken = default);
    Task<WeighTicket> AddAsync(WeighTicket ticket, CancellationToken cancellationToken = default);
    Task UpdateAsync(WeighTicket ticket, CancellationToken cancellationToken = default);
    Task<WeighEvent> AddEventAsync(WeighEvent weighEvent, CancellationToken cancellationToken = default);
    Task<int> GetNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<int> PeekNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> SearchAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<Customer?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task<Customer> UpsertAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> FindSimilarAsync(string name, int maxResults = 5, CancellationToken cancellationToken = default);
}

public interface ICargoTypeRepository
{
    Task<IReadOnlyList<CargoType>> SearchAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<CargoType?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task<CargoType> UpsertAsync(string name, CancellationToken cancellationToken = default);
}

public interface IVehicleRepository
{
    Task<Vehicle?> FindByPlateAsync(string plateNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> SearchAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<Vehicle> UpsertAsync(string plateNumber, int? customerId, CancellationToken cancellationToken = default);
    Task<VehicleSuggestion?> GetSuggestionForPlateAsync(string plateNumber, CancellationToken cancellationToken = default);
}
