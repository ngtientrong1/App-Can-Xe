using CanXe.Domain.Services;

namespace CanXe.Application.Mapping;

public static class WeightStorageMapper
{
    public static int? ToGrams(decimal? weightKg)
    {
        if (weightKg is null)
            return null;

        return (int)Math.Round(weightKg.Value * 1000m, MidpointRounding.AwayFromZero);
    }

    public static decimal? FromGrams(int? grams)
    {
        if (grams is null)
            return null;

        return grams.Value / 1000m;
    }

    public static int? ToVndPerKg(decimal? unitPrice)
    {
        if (unitPrice is null or <= 0)
            return null;

        return (int)Math.Round(unitPrice.Value, 0, MidpointRounding.AwayFromZero);
    }

    public static decimal? FromVndPerKg(int? vndPerKg)
    {
        if (vndPerKg is null)
            return null;

        return vndPerKg.Value;
    }

    public static int? ToVnd(decimal? amount)
    {
        if (amount is null)
            return null;

        return (int)Math.Round(amount.Value, 0, MidpointRounding.AwayFromZero);
    }

    public static decimal? FromVnd(int? vnd)
    {
        if (vnd is null)
            return null;

        return vnd.Value;
    }

    public static void ApplyCalculationToTicket(
        Domain.Entities.WeighTicket ticket,
        decimal? weight1Kg,
        decimal? weight2Kg,
        decimal? unitPriceVndPerKg)
    {
        var result = WeightCalculator.Calculate(weight1Kg, weight2Kg, unitPriceVndPerKg);

        ticket.GrossWeightGrams = ToGrams(result.GrossWeightKg);
        ticket.TareWeightGrams = ToGrams(result.TareWeightKg);
        ticket.NetWeightGrams = ToGrams(result.NetWeightKg);
        ticket.DeductionWeightGrams = ToGrams(result.DeductionWeightKg);
        ticket.BillableWeightGrams = ToGrams(result.BillableWeightKg);
        ticket.TotalAmountVnd = ToVnd(result.TotalAmountVnd);
    }
}
