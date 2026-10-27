using System.Globalization;
using System.Text;

namespace CanXe.Debt.Services;

public static class NameNormalizer
{
    /// <summary>Lower-case, no diacritics, punctuation and repeated spaces collapsed: "Cty. Đại  Lợi" → "cty dai loi".</summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var decomposed = name.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            var c = ch switch { 'đ' or 'Đ' => 'd', _ => char.ToLowerInvariant(ch) };
            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && sb.Length > 0)
                    sb.Append(' ');
                sb.Append(c);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return sb.ToString();
    }
}
