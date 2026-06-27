using System.Globalization;

namespace CanXe.Domain.Services;

public static class UnitPriceInputHelper
{
    public const string EditableForeground = "#1F2937";
    public const string EditableBackground = "White";

    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.Trim().Replace(" ", string.Empty);
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out var price)
            && !decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
            return null;

        price = Math.Round(price, 0, MidpointRounding.AwayFromZero);
        return price > 0 ? price : null;
    }

    public static string? FormatDisplay(decimal? value) =>
        value?.ToString("N0", CultureInfo.CurrentCulture);

    public static string? CommitDisplay(string? rawInput)
    {
        var parsed = Parse(rawInput);
        return parsed.HasValue ? FormatDisplay(parsed) : null;
    }

    public static bool IsEditableForeground(string? foregroundHex) =>
        string.Equals(foregroundHex, EditableForeground, StringComparison.OrdinalIgnoreCase);
}
