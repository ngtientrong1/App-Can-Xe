using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface ICatalogService
{
    Task<IReadOnlyList<CatalogRowItem>> ListAsync(CatalogTab tab, string? search, CancellationToken cancellationToken = default);
    Task<CatalogSaveResult> SaveCustomerAsync(CustomerCatalogEdit edit, CancellationToken cancellationToken = default);
    Task<CatalogSaveResult> SaveVehicleAsync(VehicleCatalogEdit edit, CancellationToken cancellationToken = default);
    Task<CatalogSaveResult> SaveCargoTypeAsync(CargoCatalogEdit edit, CancellationToken cancellationToken = default);
    Task HideCustomerAsync(int id, CancellationToken cancellationToken = default);
    Task HideVehicleAsync(int id, CancellationToken cancellationToken = default);
    Task HideCargoTypeAsync(int id, CancellationToken cancellationToken = default);
    Task LearnFromTicketAsync(
        string? customerName,
        string? licensePlate,
        string? cargoTypeName,
        CancellationToken cancellationToken = default);
}
