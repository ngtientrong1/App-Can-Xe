using System.Globalization;

namespace CanXe.Domain.Services;

public static class ManualSimulationInputHelper
{
    public const int MinKg = 0;
    public const int MaxKg = 999_999;

    public static bool TryParseKg(string? text, out int kg, out string? errorMessage)
    {
        kg = 0;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            errorMessage = "Nhập trọng lượng mô phỏng (kg).";
            return false;
        }

        var normalized = text.Trim()
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .Replace(",", string.Empty);
        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out kg)
            && !int.TryParse(normalized, NumberStyles.Integer, CultureInfo.CurrentCulture, out kg))
        {
            errorMessage = "Trọng lượng phải là số nguyên (kg).";
            return false;
        }

        if (kg < MinKg)
        {
            errorMessage = "Trọng lượng không được âm.";
            return false;
        }

        if (kg > MaxKg)
        {
            errorMessage = $"Trọng lượng tối đa {MaxKg:N0} kg.";
            return false;
        }

        return true;
    }
}
