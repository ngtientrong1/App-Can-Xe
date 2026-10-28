using CanXe.Debt.Services;

namespace CanXe.Debt.Tests;

public sealed class LotNamingTests
{
    private static string Name(params (string Date, string Number)[] trucks) =>
        LotNaming.Name(trucks.Select(t => (DateOnly.Parse(t.Date), t.Number)).ToList());

    [Fact]
    public void ThreeTrucksOrMore_AreALotNamedByTheirDays()
    {
        Assert.Equal("Lô 24/11", Name(("2025-11-24", "1"), ("2025-11-24", "2"), ("2025-11-24", "3")));
        Assert.Equal("Lô 22–23/10", Name(("2026-10-22", "1"), ("2026-10-22", "2"), ("2026-10-23", "3")));
        Assert.Equal("Lô 30/10 – 02/11", Name(("2026-10-30", "1"), ("2026-11-02", "2"), ("2026-11-01", "3")));
    }

    [Fact]
    public void OneOrTwoTrucks_AreNotCalledALot_JustTheirTicketsAndDay()
    {
        Assert.Equal("ngày 27/10 phiếu 57/10", Name(("2026-10-27", "57/10")));
        Assert.Equal("ngày 27/10 phiếu 57/10, 58/10", Name(("2026-10-27", "57/10"), ("2026-10-27", "58/10")));
        Assert.Equal("ngày 27/10 phiếu 57/10, ngày 28/10 phiếu 61/10", Name(("2026-10-27", "57/10"), ("2026-10-28", "61/10")));
    }

    [Fact]
    public void AnEmptyGroupHasAPlaceholderName() => Assert.Equal("Lô trống", Name());
}