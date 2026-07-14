using System.Globalization;

namespace CanXe.Application.Services;

public static class VietnameseNumberFormatter
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string FormatInteger(decimal? value) =>
        FormatRounded(value);

    public static string FormatWeight(decimal? value) =>
        FormatRounded(value);

    public static string FormatMoney(decimal? value) =>
        FormatRounded(value);

    public static string FormatInteger(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("N0", VietnameseCulture);

    private static string FormatRounded(decimal? value)
    {
        if (value is null)
            return "—";

        return Math.Round(value.Value, 0, MidpointRounding.AwayFromZero).ToString("N0", VietnameseCulture);
    }
}
