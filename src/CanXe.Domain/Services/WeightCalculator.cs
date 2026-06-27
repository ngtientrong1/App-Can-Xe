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

        var deduction = net / 1000m * 3m;
        var rawBillable = net - deduction;
        var billable = Math.Round(rawBillable, 0, MidpointRounding.AwayFromZero);
        var total = Math.Round(billable * unitPriceVndPerKg.Value, 0, MidpointRounding.AwayFromZero);

        return new(gross, tare, net, deduction, billable, total);
    }

    public static bool HasBillableUnitPrice(decimal? unitPriceVndPerKg) =>
        unitPriceVndPerKg is > 0;

    public static bool IsSingleWeigh(decimal? weight1Kg, decimal? weight2Kg) =>
        weight1Kg is null ^ weight2Kg is null;
}
