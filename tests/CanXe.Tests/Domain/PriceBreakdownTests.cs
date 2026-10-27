using CanXe.Domain.Services;

namespace CanXe.Tests.Domain;

public sealed class PriceBreakdownTests
{
    [Fact]
    public void NoSplit_IsOneLineAtTheUnitPrice()
    {
        var lines = PriceBreakdown.Compute(16020m, 19800m, split: null);

        var line = Assert.Single(lines);
        Assert.Equal(16020m, line.WeightKg);
        Assert.Equal(19800m, line.UnitPriceVndPerKg);
        Assert.Equal(16020m * 19800m, line.AmountVnd);
    }

    [Fact]
    public void TwoLevels_FirstPartAtFirstPriceRestAtSecondPrice()
    {
        var lines = PriceBreakdown.Compute(16020m, 19800m, new PriceSplit(15000m, 19600m));

        Assert.Equal(2, lines.Count);
        Assert.Equal(new PriceLine(15000m, 19800m, 297_000_000m), lines[0]);
        Assert.Equal(new PriceLine(1020m, 19600m, 19_992_000m), lines[1]);
        Assert.Equal(316_992_000m, PriceBreakdown.TotalAmount(lines));
    }

    [Theory]
    [InlineData(15000)]
    [InlineData(16020)]
    [InlineData(20000)]
    public void FirstPartCoveringTheWholeWeight_CollapsesToOneLine(int firstKg)
    {
        var lines = PriceBreakdown.Compute(15000m, 19800m, new PriceSplit(firstKg, 19600m));

        var line = Assert.Single(lines);
        Assert.Equal(19800m, line.UnitPriceVndPerKg);
        Assert.Equal(15000m, line.WeightKg);
    }

    [Fact]
    public void InvalidSplit_IsIgnored()
    {
        Assert.Single(PriceBreakdown.Compute(16020m, 19800m, new PriceSplit(0m, 19600m)));
        Assert.Single(PriceBreakdown.Compute(16020m, 19800m, new PriceSplit(15000m, 0m)));
        Assert.Null(PriceSplit.TryCreate(null, 19600m));
        Assert.Null(PriceSplit.TryCreate(15000m, null));
    }

    [Fact]
    public void NoBillableWeightOrNoPrice_HasNoLines()
    {
        Assert.Empty(PriceBreakdown.Compute(0m, 19800m, new PriceSplit(15000m, 19600m)));
        Assert.Empty(PriceBreakdown.Compute(16020m, 0m, new PriceSplit(15000m, 19600m)));
    }

    [Fact]
    public void AverageUnitPrice_IsTotalOverWeight()
    {
        var lines = PriceBreakdown.Compute(16020m, 19800m, new PriceSplit(15000m, 19600m));

        Assert.Equal(19787m, PriceBreakdown.AverageUnitPrice(lines));
    }

    [Fact]
    public void Calculator_UsesTheSplitForTheTotal()
    {
        // 16,070 kg net, 0.3 percent deduction -> 16,022 kg billable.
        var split = new PriceSplit(15000m, 19600m);

        var result = WeightCalculator.Calculate(5000m, 21070m, 19800m, split);

        Assert.Equal(16022m, result.BillableWeightKg);
        Assert.Equal(15000m * 19800m + 1022m * 19600m, result.TotalAmountVnd);
    }

    [Fact]
    public void Calculator_WithoutSplit_MatchesTheOldSinglePriceResult()
    {
        var single = WeightCalculator.Calculate(5000m, 21070m, 19800m);
        var withNullSplit = WeightCalculator.Calculate(5000m, 21070m, 19800m, split: null);

        Assert.Equal(single, withNullSplit);
        Assert.Equal(16022m * 19800m, single.TotalAmountVnd);
    }
}
