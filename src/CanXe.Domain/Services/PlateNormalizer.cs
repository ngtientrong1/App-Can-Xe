using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CanXe.Domain.Services;

public static class PlateNormalizer
{
    // Standard Vietnamese car plate: 2-digit province + 1 letter series + optional series digit +
    // 5-digit tail, e.g. "51C12345" or "51C112345". Anything else (motorbikes, older/foreign
    // formats, partial input) falls back to the plain uppercase form rather than being mangled.
    private static readonly Regex CarPlatePattern = new(
        @"^(?<province>\d{2})(?<series>[A-Z])(?<seriesDigit>\d)?(?<tail>\d{5})$",
        RegexOptions.Compiled);

    public static string Normalize(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return string.Empty;

        var builder = new StringBuilder(plate.Length);
        foreach (var ch in plate.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
        }

        return builder.ToString();
    }

    public static string FormatDisplay(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return string.Empty;

        // Only recognized standard car plates get the "PP L-DDD.DD" treatment; anything else
        // (motorbike plates, foreign/legacy formats, or non-plate identifiers) keeps its original
        // punctuation rather than having it stripped by Normalize().
        var trimmedUpper = plate.Trim().ToUpperInvariant();
        var normalized = Normalize(plate);
        var match = CarPlatePattern.Match(normalized);
        if (!match.Success)
            return trimmedUpper;

        var series = match.Groups["series"].Value + match.Groups["seriesDigit"].Value;
        var tail = match.Groups["tail"].Value;
        return $"{match.Groups["province"].Value}{series}-{tail[..3]}.{tail[3..]}";
    }
}
