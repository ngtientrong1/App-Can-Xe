using System.Globalization;

namespace CanXe.Domain.Services;

/// <summary>Display text for single and two-level prices, shared by the grid, reports and printed ticket.</summary>
public static class PriceTextFormatter
{
    public static string UnitPrice(decimal? first, decimal? second, bool hasSplit)
    {
        if (hasSplit && first is > 0 && second is > 0)
            return $"{Number(first.Value)} / {Number(second.Value)}";

        return first.HasValue ? Number(first.Value) : "—";
    }

    /// <summary>"15.000 kg × 19.800 + 1.020 kg × 19.600" for the lines of a split ticket.</summary>
    public static string Breakdown(IReadOnlyList<PriceLine> lines) =>
        string.Join("  +  ", lines.Select(l => $"{Number(l.WeightKg)} kg × {Number(l.UnitPriceVndPerKg)}"));

    private static string Number(decimal value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
