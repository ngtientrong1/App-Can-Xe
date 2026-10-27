namespace CanXe.Domain.Services;

/// <summary>
/// Two-level pricing for one ticket: the first <see cref="FirstWeightKg"/> of the billable weight is
/// charged at the ticket's normal unit price, the rest at <see cref="SecondUnitPriceVndPerKg"/>.
/// </summary>
public sealed record PriceSplit(decimal FirstWeightKg, decimal SecondUnitPriceVndPerKg)
{
    public bool IsValid => FirstWeightKg > 0 && SecondUnitPriceVndPerKg > 0;

    public static PriceSplit? TryCreate(decimal? firstWeightKg, decimal? secondUnitPriceVndPerKg) =>
        firstWeightKg is > 0 && secondUnitPriceVndPerKg is > 0
            ? new PriceSplit(firstWeightKg.Value, secondUnitPriceVndPerKg.Value)
            : null;
}

public sealed record PriceLine(decimal WeightKg, decimal UnitPriceVndPerKg, decimal AmountVnd);

public static class PriceBreakdown
{
    /// <summary>
    /// The priced lines of a ticket: one line normally, two when a valid split applies. A split whose
    /// first part covers the whole billable weight collapses to a single line at the first price.
    /// </summary>
    public static IReadOnlyList<PriceLine> Compute(decimal billableKg, decimal firstUnitPriceVndPerKg, PriceSplit? split)
    {
        if (billableKg <= 0 || firstUnitPriceVndPerKg <= 0)
            return [];

        if (split is not { IsValid: true } || split.FirstWeightKg >= billableKg)
            return [Line(billableKg, firstUnitPriceVndPerKg)];

        return
        [
            Line(split.FirstWeightKg, firstUnitPriceVndPerKg),
            Line(billableKg - split.FirstWeightKg, split.SecondUnitPriceVndPerKg)
        ];
    }

    public static decimal TotalAmount(IEnumerable<PriceLine> lines) => lines.Sum(l => l.AmountVnd);

    /// <summary>Total divided by weight, for places that can only show one price per kg.</summary>
    public static decimal? AverageUnitPrice(IReadOnlyList<PriceLine> lines)
    {
        var weight = lines.Sum(l => l.WeightKg);
        return weight > 0 ? Math.Round(TotalAmount(lines) / weight, 0, MidpointRounding.AwayFromZero) : null;
    }

    private static PriceLine Line(decimal weightKg, decimal unitPrice) =>
        new(weightKg, unitPrice, Math.Round(weightKg * unitPrice, 0, MidpointRounding.AwayFromZero));
}
