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

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    public static bool ContainsNormalized(string source, string search)
    {
        var normalizedSearch = Normalize(search);
        if (normalizedSearch.Length == 0)
            return true;

        return Normalize(source).Contains(normalizedSearch, StringComparison.Ordinal);
    }
}
