using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public static class FilterSummaryCalculator
{
    public static WeighTicketFilterResult Build(IReadOnlyList<WeighTicketListItem> items) =>
        new()
        {
            Items = items,
            Count = items.Count,
            TotalNetWeightKg = items.Where(i => i.NetWeightKg.HasValue).Sum(i => i.NetWeightKg!.Value),
            TotalBillableWeightKg = items.Where(i => i.BillableWeightKg.HasValue).Sum(i => i.BillableWeightKg!.Value),
            TotalAmountVnd = items.Where(i => i.TotalAmountVnd.HasValue).Sum(i => i.TotalAmountVnd!.Value),
            MissingPriceCount = items.Count(i =>
                i.NetWeightKg.HasValue && !WeightCalculator.HasBillableUnitPrice(i.UnitPriceVndPerKg))
        };
}
