using System.Globalization;

namespace CanXe.Debt.Services;

/// <summary>How a group of trucks is named on screen and on paper: more than two trucks make a lot named by its days; one or two trucks are just shown by their day and ticket ("ngày 27/10 phiếu 57/10").</summary>
public static class LotNaming
{
    public const int MinTrucksForLot = 3;

    public static string Name(IReadOnlyList<(DateOnly Date, string Number)> trucks)
    {
        if (trucks.Count == 0)
            return "Lô trống";

        static string Dm(DateOnly d) => d.ToString("dd/MM", CultureInfo.InvariantCulture);
        static string D(DateOnly d) => d.ToString("dd", CultureInfo.InvariantCulture);

        var ordered = trucks.OrderBy(t => t.Date).ToList();
        var first = ordered[0].Date;
        var last = ordered[^1].Date;

        if (trucks.Count >= MinTrucksForLot)
        {
            if (first == last)
                return $"Lô {Dm(first)}";
            return first.Month == last.Month && first.Year == last.Year
                ? $"Lô {D(first)}–{Dm(last)}"
                : $"Lô {Dm(first)} – {Dm(last)}";
        }

        return first == last
            ? $"ngày {Dm(first)} phiếu {string.Join(", ", ordered.Select(t => t.Number))}"
            : string.Join(", ", ordered.Select(t => $"ngày {Dm(t.Date)} phiếu {t.Number}"));
    }
}