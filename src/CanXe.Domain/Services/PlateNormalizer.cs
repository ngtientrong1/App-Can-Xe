using System.Globalization;
using System.Text;

namespace CanXe.Domain.Services;

public static class PlateNormalizer
{
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

        return plate.Trim().ToUpperInvariant();
    }
}
