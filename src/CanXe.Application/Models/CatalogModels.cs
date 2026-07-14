namespace CanXe.Application.Models;

public enum CatalogTab
{
    Customer,
    Vehicle,
    CargoType
}

public sealed class CatalogRowItem
{
    public int Id { get; init; }
    public CatalogTab Tab { get; init; }
    public string PrimaryName { get; init; } = string.Empty;
    public string? SecondaryName { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? Note { get; init; }
    public decimal? DefaultUnitPrice { get; init; }
    public string? Unit { get; init; }
    public int TicketUsageCount { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public bool IsActive { get; init; }
    public string StatusText => IsActive ? "Hoạt động" : "Ẩn";
}

public sealed class CustomerCatalogEdit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class VehicleCatalogEdit
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CargoCatalogEdit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? DefaultUnitPrice { get; set; }
    public string Unit { get; set; } = "kg";
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CatalogSaveResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static CatalogSaveResult Ok() => new() { Success = true };
    public static CatalogSaveResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
