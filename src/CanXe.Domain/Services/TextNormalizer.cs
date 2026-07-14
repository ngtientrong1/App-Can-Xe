using System.Globalization;
using System.Text;

namespace CanXe.Domain.Services;

public static class TextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        var result = builder.ToString().Normalize(NormalizationForm.FormC);
        return result
            .Replace('Đ', 'D')
            .Replace('đ', 'd')
            .ToUpperInvariant();
    }

    public static bool ContainsNormalized(string source, string search)
    {
        var normalizedSearch = Normalize(search);
        if (normalizedSearch.Length == 0)
            return true;

        return Normalize(source).Contains(normalizedSearch, StringComparison.Ordinal);
    }

    public static string CollapseSpaces(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
