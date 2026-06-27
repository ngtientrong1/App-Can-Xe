using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Tests.Domain;

public class FrequentCargoTypeResolverTests
{
    private static readonly DateTimeOffset BaseDate = new(2026, 6, 27, 12, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Resolve_PicksMostFrequentCargoType()
    {
        var tickets = new List<CargoUsageTicketRow>
        {
            new(1, "Rô tươi", BaseDate),
            new(1, "Rô tươi", BaseDate.AddDays(-1)),
            new(2, "Cà tươi", BaseDate.AddDays(-2))
        };

        var result = FrequentCargoTypeResolver.Resolve(tickets);

        Assert.NotNull(result);
        Assert.Equal("Rô tươi", result.Value.CargoTypeName);
        Assert.Equal(2, result.Value.UsageCount);
    }

    [Fact]
    public void Resolve_WhenCountsEqual_PicksMostRecentCargoType()
    {
        var tickets = new List<CargoUsageTicketRow>
        {
            new(1, "Rô tươi", BaseDate.AddDays(-1)),
            new(2, "Cà tươi", BaseDate),
            new(1, "Rô tươi", BaseDate.AddDays(-2)),
            new(2, "Cà tươi", BaseDate.AddDays(-3))
        };

        var result = FrequentCargoTypeResolver.Resolve(tickets);

        Assert.NotNull(result);
        Assert.Equal("Cà tươi", result.Value.CargoTypeName);
    }

    [Fact]
    public void Resolve_IgnoresNullCargoTypeRows()
    {
        var tickets = new List<CargoUsageTicketRow>
        {
            new(null, null, BaseDate),
            new(null, "  ", BaseDate.AddDays(-1)),
            new(1, "Rô tươi", BaseDate.AddDays(-2))
        };

        var result = FrequentCargoTypeResolver.Resolve(tickets);

        Assert.NotNull(result);
        Assert.Equal("Rô tươi", result.Value.CargoTypeName);
    }
}

public class WorkAreaLayoutCalculatorTests
{
    [Fact]
    public void CameraCollapsed_Uses34_66_0Ratio()
    {
        var stars = WorkAreaLayoutCalculator.GetColumnStars(false);
        Assert.Equal((34, 66, 0), stars);
        Assert.False(WorkAreaLayoutCalculator.HasUnusedCameraColumnGap(false, 0));
    }

    [Fact]
    public void CameraVisible_Uses32_50_18Ratio()
    {
        var stars = WorkAreaLayoutCalculator.GetColumnStars(true);
        Assert.Equal((32, 50, 18), stars);
    }
}
