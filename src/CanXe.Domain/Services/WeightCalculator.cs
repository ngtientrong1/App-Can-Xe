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
    public static WeightCalculationResult Calculate(decimal? weight1Kg, decimal? weight2Kg, decimal? unitPriceVndPerKg) =>
        Calculate(weight1Kg, weight2Kg, unitPriceVndPerKg, split: null);

    /// <summary>
    /// Same as the single-price calculation, but when <paramref name="split"/> is valid the billable
    /// weight is priced in two parts (first part at the unit price, the rest at the second price).
    /// </summary>
    public static WeightCalculationResult Calculate(
        decimal? weight1Kg, decimal? weight2Kg, decimal? unitPriceVndPerKg, PriceSplit? split)
    {
        if (weight1Kg is null && weight2Kg is null)
            return new(null, null, null, null, null, null);

        decimal gross;
        decimal tare;
        decimal net;

        if (weight1Kg is null ^ weight2Kg is null)
        {
            var single = weight1Kg ?? weight2Kg!.Value;
            gross = single;
            tare = 0m;
            net = single;
        }
        else
        {
            gross = Math.Max(weight1Kg!.Value, weight2Kg!.Value);
            tare = Math.Min(weight1Kg.Value, weight2Kg.Value);
            net = Math.Abs(weight1Kg.Value - weight2Kg.Value);
        }

        if (unitPriceVndPerKg is not > 0)
            return new(gross, tare, net, null, null, null);

        // Each priced part carries its own deduction, so the billable weight is the sum of the parts.
        var deduction = net / 1000m * PriceBreakdown.DeductionPerMille;
        var lines = PriceBreakdown.Compute(net, unitPriceVndPerKg.Value, split);
        var billable = lines.Sum(l => l.WeightKg);
        var total = PriceBreakdown.TotalAmount(lines);

        return new(gross, tare, net, deduction, billable, total);
    }

    public static bool HasBillableUnitPrice(decimal? unitPriceVndPerKg) =>
        unitPriceVndPerKg is > 0;

    public static bool IsSingleWeigh(decimal? weight1Kg, decimal? weight2Kg) =>
        weight1Kg is null ^ weight2Kg is null;
}
