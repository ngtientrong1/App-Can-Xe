namespace CanXe.Domain.Services;

public sealed record WeightCalculationResult(
    decimal? GrossWeightKg,
    decimal? TareWeightKg,
    decimal? NetWeightKg,
    decimal? DeductionWeightKg,
    decimal? BillableWeightKg,
    decimal? TotalAmountVnd);

public static class WeightCalculator
{
    public static WeightCalculationResult Calculate(decimal? weight1Kg, decimal? weight2Kg, decimal? unitPriceVndPerKg)
    {
        if (weight1Kg is null || weight2Kg is null)
            return new(null, null, null, null, null, null);

        var gross = Math.Max(weight1Kg.Value, weight2Kg.Value);
        var tare = Math.Min(weight1Kg.Value, weight2Kg.Value);
        var net = Math.Abs(weight1Kg.Value - weight2Kg.Value);

        if (unitPriceVndPerKg is not > 0)
            return new(gross, tare, net, null, null, null);

        var deduction = net / 1000m * 3m;
        var rawBillable = net - deduction;
        var billable = Math.Round(rawBillable, 0, MidpointRounding.AwayFromZero);
        var total = Math.Round(billable * unitPriceVndPerKg.Value, 0, MidpointRounding.AwayFromZero);

        return new(gross, tare, net, deduction, billable, total);
    }

    public static bool HasBillableUnitPrice(decimal? unitPriceVndPerKg) =>
        unitPriceVndPerKg is > 0;
}
