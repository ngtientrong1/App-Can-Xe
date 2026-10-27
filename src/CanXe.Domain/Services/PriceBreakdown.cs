namespace CanXe.Domain.Services;

/// <summary>
/// Two-level pricing for one ticket: the first <see cref="FirstWeightKg"/> kg of the goods (the net weight, "hàng")
/// are charged at the ticket's normal unit price and the rest at <see cref="SecondUnitPriceVndPerKg"/>. The 3 per mille
/// deduction applies to each part on its own, exactly like the hand-kept price sheets.
/// </summary>
public sealed record PriceSplit(decimal FirstWeightKg, decimal SecondUnitPriceVndPerKg)
{
    public bool IsValid => FirstWeightKg > 0 && SecondUnitPriceVndPerKg > 0;

    public static PriceSplit? TryCreate(decimal? firstWeightKg, decimal? secondUnitPriceVndPerKg) =>
        firstWeightKg is > 0 && secondUnitPriceVndPerKg is > 0
            ? new PriceSplit(firstWeightKg.Value, secondUnitPriceVndPerKg.Value)
            : null;
}

/// <param name="WeightKg">The billable weight of this part (after the deduction).</param>
public sealed record PriceLine(decimal WeightKg, decimal UnitPriceVndPerKg, decimal AmountVnd);

public static class PriceBreakdown
{
    public const decimal DeductionPerMille = 3m;

    /// <summary>Net weight minus the 3 per mille deduction, rounded to a whole kg.</summary>
    public static decimal BillableOf(decimal netKg) =>
        Math.Round(netKg - netKg / 1000m * DeductionPerMille, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The priced lines of a ticket: one line normally, two when a valid split applies. A split whose
    /// first part covers the whole net weight collapses to a single line at the first price.
    /// </summary>
    public static IReadOnlyList<PriceLine> Compute(decimal netKg, decimal firstUnitPriceVndPerKg, PriceSplit? split)
    {
        if (netKg <= 0 || firstUnitPriceVndPerKg <= 0)
            return [];

        if (split is not { IsValid: true } || split.FirstWeightKg >= netKg)
            return [Line(BillableOf(netKg), firstUnitPriceVndPerKg)];

        return
        [
            Line(BillableOf(split.FirstWeightKg), firstUnitPriceVndPerKg),
            Line(BillableOf(netKg - split.FirstWeightKg), split.SecondUnitPriceVndPerKg)
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
