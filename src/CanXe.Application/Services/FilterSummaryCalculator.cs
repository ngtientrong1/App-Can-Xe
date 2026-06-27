using CanXe.Application.Models;

namespace CanXe.Application.Services;

public static class FilterSummaryCalculator
{
    public static WeighTicketFilterResult Build(IReadOnlyList<WeighTicketListItem> items) =>
        new()
        {
            Items = items,
            Count = items.Count,
            TotalNetWeightKg = items.Sum(i => i.NetWeightKg ?? 0m),
            TotalAmountVnd = items.Sum(i => i.TotalAmountVnd ?? 0m)
        };
}
