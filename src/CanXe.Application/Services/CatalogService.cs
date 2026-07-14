using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public sealed class CatalogService : ICatalogService
{
    private readonly ICustomerRepository _customers;
    private readonly IVehicleRepository _vehicles;
    private readonly ICargoTypeRepository _cargoTypes;

    public CatalogService(
        ICustomerRepository customers,
        IVehicleRepository vehicles,
        ICargoTypeRepository cargoTypes)
    {
        _customers = customers;
        _vehicles = vehicles;
        _cargoTypes = cargoTypes;
    }

    public async Task<IReadOnlyList<CatalogRowItem>> ListAsync(
        CatalogTab tab,
        string? search,
        CancellationToken cancellationToken = default) =>
        tab switch
        {
            CatalogTab.Customer => await ListCustomersAsync(search, cancellationToken),
            CatalogTab.Vehicle => await ListVehiclesAsync(search, cancellationToken),
            CatalogTab.CargoType => await ListCargoAsync(search, cancellationToken),
            _ => []
        };

    public async Task<CatalogSaveResult> SaveCustomerAsync(
        CustomerCatalogEdit edit,
        CancellationToken cancellationToken = default)
    {
        var name = TextNormalizer.CollapseSpaces(edit.Name?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            return CatalogSaveResult.Fail("Tên khách hàng là bắt buộc.");

        var normalized = TextNormalizer.Normalize(name);
        var duplicate = await _customers.FindActiveByNormalizedNameAsync(normalized, cancellationToken);
        if (duplicate is not null && duplicate.Id != edit.Id)
            return CatalogSaveResult.Fail("Khách hàng đã tồn tại.");

        var entity = edit.Id > 0
            ? await _customers.GetByIdAsync(edit.Id, cancellationToken)
            : null;
        entity ??= new Customer
        {
            Name = name,
            NormalizedName = normalized,
            CreatedAt = DateTimeOffset.Now
        };

        entity.Name = name;
        entity.NormalizedName = normalized;
        entity.Phone = NullIfWhiteSpace(edit.Phone);
        entity.Address = NullIfWhiteSpace(edit.Address);
        entity.Note = NullIfWhiteSpace(edit.Note);
        entity.IsActive = edit.IsActive;
        entity.IsDeleted = false;
        entity.DeletedAt = null;

        await _customers.SaveCatalogAsync(entity, cancellationToken);
        return CatalogSaveResult.Ok();
    }

    public async Task<CatalogSaveResult> SaveVehicleAsync(
        VehicleCatalogEdit edit,
        CancellationToken cancellationToken = default)
    {
        var plate = PlateNormalizer.FormatDisplay(edit.LicensePlate);
        if (string.IsNullOrWhiteSpace(plate))
            return CatalogSaveResult.Fail("Biển số là bắt buộc.");

        var normalized = PlateNormalizer.Normalize(plate);
        var duplicate = await _vehicles.FindActiveByPlateAsync(plate, cancellationToken);
        if (duplicate is not null && duplicate.Id != edit.Id)
            return CatalogSaveResult.Fail("Biển số đã tồn tại.");

        var entity = edit.Id > 0
            ? await _vehicles.GetByIdAsync(edit.Id, cancellationToken)
            : null;
        entity ??= new Vehicle
        {
            PlateNumber = plate,
            NormalizedPlateNumber = normalized,
            CreatedAt = DateTimeOffset.Now
        };

        entity.PlateNumber = plate;
        entity.NormalizedPlateNumber = normalized;
        entity.OwnerName = NullIfWhiteSpace(edit.OwnerName);
        entity.Note = NullIfWhiteSpace(edit.Note);
        entity.IsActive = edit.IsActive;
        entity.IsDeleted = false;
        entity.DeletedAt = null;

        await _vehicles.SaveCatalogAsync(entity, cancellationToken);
        return CatalogSaveResult.Ok();
    }

    public async Task<CatalogSaveResult> SaveCargoTypeAsync(
        CargoCatalogEdit edit,
        CancellationToken cancellationToken = default)
    {
        var name = TextNormalizer.CollapseSpaces(edit.Name?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            return CatalogSaveResult.Fail("Tên loại hàng là bắt buộc.");

        if (edit.DefaultUnitPrice is < 0)
            return CatalogSaveResult.Fail("Đơn giá mặc định không hợp lệ.");

        var normalized = TextNormalizer.Normalize(name);
        var duplicate = await _cargoTypes.FindActiveByNormalizedNameAsync(normalized, cancellationToken);
        if (duplicate is not null && duplicate.Id != edit.Id)
            return CatalogSaveResult.Fail("Loại hàng đã tồn tại.");

        var entity = edit.Id > 0
            ? await _cargoTypes.GetByIdAsync(edit.Id, cancellationToken)
            : null;
        entity ??= new CargoType
        {
            Name = name,
            NormalizedName = normalized,
            CreatedAt = DateTimeOffset.Now,
            Unit = "kg"
        };

        entity.Name = name;
        entity.NormalizedName = normalized;
        entity.Note = NullIfWhiteSpace(edit.Note);
        entity.Unit = string.IsNullOrWhiteSpace(edit.Unit) ? "kg" : edit.Unit.Trim();
        entity.DefaultUnitPriceVndPerKg = edit.DefaultUnitPrice is decimal price and > 0
            ? (int)Math.Round(price, 0, MidpointRounding.AwayFromZero)
            : null;
        entity.IsActive = edit.IsActive;
        entity.IsDeleted = false;
        entity.DeletedAt = null;

        await _cargoTypes.SaveCatalogAsync(entity, cancellationToken);
        return CatalogSaveResult.Ok();
    }

    public Task HideCustomerAsync(int id, CancellationToken cancellationToken = default) =>
        _customers.SoftDeleteAsync(id, cancellationToken);

    public Task HideVehicleAsync(int id, CancellationToken cancellationToken = default) =>
        _vehicles.SoftDeleteAsync(id, cancellationToken);

    public Task HideCargoTypeAsync(int id, CancellationToken cancellationToken = default) =>
        _cargoTypes.SoftDeleteAsync(id, cancellationToken);

    public async Task LearnFromTicketAsync(
        string? customerName,
        string? licensePlate,
        string? cargoTypeName,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(customerName))
        {
            try
            {
                var normalized = TextNormalizer.Normalize(customerName.Trim());
                if (normalized.Length > 0
                    && await _customers.FindActiveByNormalizedNameAsync(normalized, cancellationToken) is null)
                {
                    await _customers.UpsertAsync(customerName, cancellationToken);
                }
            }
            catch
            {
                // Auto-learn must not fail ticket save.
            }
        }

        if (!string.IsNullOrWhiteSpace(licensePlate))
        {
            try
            {
                var plate = PlateNormalizer.FormatDisplay(licensePlate);
                if (!string.IsNullOrWhiteSpace(plate)
                    && await _vehicles.FindActiveByPlateAsync(plate, cancellationToken) is null)
                {
                    await _vehicles.UpsertAsync(plate, null, cancellationToken);
                }
            }
            catch
            {
            }
        }

        if (!string.IsNullOrWhiteSpace(cargoTypeName))
        {
            try
            {
                var normalized = TextNormalizer.Normalize(cargoTypeName.Trim());
                if (normalized.Length > 0
                    && await _cargoTypes.FindActiveByNormalizedNameAsync(normalized, cancellationToken) is null)
                {
                    await _cargoTypes.UpsertAsync(cargoTypeName, cancellationToken);
                }
            }
            catch
            {
            }
        }
    }

    private async Task<IReadOnlyList<CatalogRowItem>> ListCustomersAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var rows = await _customers.ListCatalogAsync(search, cancellationToken);
        var items = new List<CatalogRowItem>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(new CatalogRowItem
            {
                Id = row.Id,
                Tab = CatalogTab.Customer,
                PrimaryName = row.Name,
                Phone = row.Phone,
                Address = row.Address,
                Note = row.Note,
                TicketUsageCount = await _customers.CountTicketUsageAsync(row.Id, cancellationToken),
                LastUsedAt = row.LastUsedAt,
                IsActive = row.IsActive && !row.IsDeleted
            });
        }

        return items;
    }

    private async Task<IReadOnlyList<CatalogRowItem>> ListVehiclesAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var rows = await _vehicles.ListCatalogAsync(search, cancellationToken);
        var items = new List<CatalogRowItem>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(new CatalogRowItem
            {
                Id = row.Id,
                Tab = CatalogTab.Vehicle,
                PrimaryName = row.PlateNumber,
                SecondaryName = row.OwnerName ?? row.LastCustomer?.Name,
                Note = row.Note,
                TicketUsageCount = await _vehicles.CountTicketUsageAsync(row.Id, cancellationToken),
                LastUsedAt = row.LastUsedAt,
                IsActive = row.IsActive && !row.IsDeleted
            });
        }

        return items;
    }

    private async Task<IReadOnlyList<CatalogRowItem>> ListCargoAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var rows = await _cargoTypes.ListCatalogAsync(search, cancellationToken);
        var items = new List<CatalogRowItem>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(new CatalogRowItem
            {
                Id = row.Id,
                Tab = CatalogTab.CargoType,
                PrimaryName = row.Name,
                DefaultUnitPrice = row.DefaultUnitPriceVndPerKg,
                Unit = row.Unit,
                Note = row.Note,
                TicketUsageCount = await _cargoTypes.CountTicketUsageAsync(row.Id, cancellationToken),
                LastUsedAt = row.LastUsedAt,
                IsActive = row.IsActive && !row.IsDeleted
            });
        }

        return items;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
