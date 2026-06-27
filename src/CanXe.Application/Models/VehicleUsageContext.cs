namespace CanXe.Application.Models;

public sealed class VehicleUsageContext
{
    public int VehicleId { get; init; }
    public required string PlateNumber { get; init; }
    public int? RecentCustomerId { get; init; }
    public string? RecentCustomerName { get; init; }
    public int? RecentCargoTypeId { get; init; }
    public string? RecentCargoTypeName { get; init; }
    public int? FrequentCargoTypeId { get; init; }
    public string? FrequentCargoTypeName { get; init; }
    public int FrequentCargoUsageCount { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
}
