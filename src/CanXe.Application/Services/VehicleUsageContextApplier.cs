using CanXe.Application.Models;

namespace CanXe.Application.Services;

public enum VehicleContextApplyMode
{
    Both,
    CustomerOnly,
    CargoOnly
}

public sealed record VehicleContextApplyResult
{
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public int? CargoTypeId { get; init; }
    public string? CargoTypeName { get; init; }
    public bool CustomerChanged { get; init; }
    public bool CargoTypeChanged { get; init; }
}

public static class VehicleUsageContextApplier
{
    public static VehicleContextApplyResult Apply(
        VehicleUsageContext context,
        VehicleContextApplyMode mode,
        string? currentCustomerName,
        string? currentCargoTypeName)
    {
        var result = new VehicleContextApplyResult();

        if (mode is VehicleContextApplyMode.Both or VehicleContextApplyMode.CustomerOnly)
        {
            if (context.RecentCustomerName is { } customerName)
            {
                result = result with
                {
                    CustomerId = context.RecentCustomerId,
                    CustomerName = customerName,
                    CustomerChanged = !string.Equals(
                        currentCustomerName?.Trim(),
                        customerName,
                        StringComparison.OrdinalIgnoreCase)
                };
            }
        }

        if (mode is VehicleContextApplyMode.Both or VehicleContextApplyMode.CargoOnly)
        {
            var cargoName = context.FrequentCargoTypeName ?? context.RecentCargoTypeName;
            var cargoId = context.FrequentCargoTypeId ?? context.RecentCargoTypeId;
            if (cargoName is not null)
            {
                result = result with
                {
                    CargoTypeId = cargoId,
                    CargoTypeName = cargoName,
                    CargoTypeChanged = !string.Equals(
                        currentCargoTypeName?.Trim(),
                        cargoName,
                        StringComparison.OrdinalIgnoreCase)
                };
            }
        }

        return result;
    }

    public static string? BuildCustomerChangePreview(
        VehicleUsageContext context,
        string? currentCustomerName) =>
        context.RecentCustomerName is { } suggested &&
        !string.IsNullOrWhiteSpace(currentCustomerName) &&
        !string.Equals(currentCustomerName.Trim(), suggested, StringComparison.OrdinalIgnoreCase)
            ? $"Khách: {currentCustomerName.Trim()} → {suggested}"
            : null;

    public static string? BuildCargoChangePreview(
        VehicleUsageContext context,
        string? currentCargoTypeName)
    {
        var suggested = context.FrequentCargoTypeName ?? context.RecentCargoTypeName;
        return suggested is not null &&
               !string.IsNullOrWhiteSpace(currentCargoTypeName) &&
               !string.Equals(currentCargoTypeName.Trim(), suggested, StringComparison.OrdinalIgnoreCase)
            ? $"Loại hàng: {currentCargoTypeName.Trim()} → {suggested}"
            : null;
    }

    public static bool ShouldHighlightApplyBoth(string? currentCustomerName, string? currentCargoTypeName) =>
        string.IsNullOrWhiteSpace(currentCustomerName) && string.IsNullOrWhiteSpace(currentCargoTypeName);
}
