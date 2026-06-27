using CanXe.Application.Models;

namespace CanXe.Application.Services;

public static class ActiveFilterChipBuilder
{
    public static IReadOnlyList<ActiveFilterChip> Build(WeighTicketFilter filter, string? quickRangeLabel = null)
    {
        var chips = new List<ActiveFilterChip>();

        if (!string.IsNullOrWhiteSpace(quickRangeLabel))
            chips.Add(new ActiveFilterChip { Key = "quickRange", Label = quickRangeLabel });

        if (filter.FromDate is not null || filter.ToDate is not null)
        {
            var from = filter.FromDate?.ToString("dd/MM/yyyy") ?? "…";
            var to = filter.ToDate?.ToString("dd/MM/yyyy") ?? "…";
            if (string.IsNullOrWhiteSpace(quickRangeLabel))
                chips.Add(new ActiveFilterChip { Key = "dateRange", Label = $"{from} – {to}" });
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            chips.Add(new ActiveFilterChip { Key = "customer", Label = $"Khách hàng: {filter.CustomerName}" });

        if (!string.IsNullOrWhiteSpace(filter.CargoTypeName))
            chips.Add(new ActiveFilterChip { Key = "cargo", Label = $"Loại hàng: {filter.CargoTypeName}" });

        if (!string.IsNullOrWhiteSpace(filter.LicensePlate))
            chips.Add(new ActiveFilterChip { Key = "plate", Label = $"Biển số: {filter.LicensePlate}" });

        if (!string.IsNullOrWhiteSpace(filter.DisplayNumber))
            chips.Add(new ActiveFilterChip { Key = "ticketNo", Label = $"Số phiếu: {filter.DisplayNumber}" });

        if (filter.UnitPriceVndPerKg is { } exact)
            chips.Add(new ActiveFilterChip { Key = "unitPrice", Label = $"Đơn giá: {exact:N0}" });
        else if (filter.UnitPriceFromVndPerKg is not null || filter.UnitPriceToVndPerKg is not null)
        {
            var from = filter.UnitPriceFromVndPerKg?.ToString("N0") ?? "…";
            var to = filter.UnitPriceToVndPerKg?.ToString("N0") ?? "…";
            chips.Add(new ActiveFilterChip { Key = "unitPriceRange", Label = $"Đơn giá: {from} – {to}" });
        }

        return chips;
    }
}
