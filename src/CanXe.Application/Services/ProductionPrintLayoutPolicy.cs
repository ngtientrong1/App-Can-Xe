using CanXe.Application.Models;

namespace CanXe.Application.Services;

/// <summary>
/// Production print always uses canonical A4 two-up (rc23). A5SingleTicket is legacy/non-production.
/// </summary>
public static class ProductionPrintLayoutPolicy
{
    public const PrintLayoutMode ProductionLayoutMode = PrintLayoutMode.A4TwoUp;

    public static PrintLayoutMode ResolveProductionLayoutMode(PrintSettingsDto settings)
    {
        _ = settings;
        return ProductionLayoutMode;
    }

    public static PrintSettingsDto NormalizeForProduction(PrintSettingsDto settings)
    {
        settings.PrintLayoutMode = ProductionLayoutMode;
        return settings;
    }

    public static string NormalizeStoredLayoutMode(string? stored) =>
        string.Equals(stored, nameof(PrintLayoutMode.A5SingleTicket), StringComparison.OrdinalIgnoreCase)
            ? nameof(PrintLayoutMode.A4TwoUp)
            : string.IsNullOrWhiteSpace(stored)
                ? nameof(PrintLayoutMode.A4TwoUp)
                : stored;
}
