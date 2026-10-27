using CanXe.Domain.Services;

namespace CanXe.Tests.Domain;

public sealed class PriceBreakdownTests
{
    [Fact]
    public void NoSplit_IsOneLineBilledAfterTheDeduction()
    {
        var lines = PriceBreakdown.Compute(16020m, 19800m, split: null);

        var line = Assert.Single(lines);
        Assert.Equal(15972m, line.WeightKg); // 16,020 − 3 per mille = 15,971.94 → 15,972
        Assert.Equal(19800m, line.UnitPriceVndPerKg);
        Assert.Equal(15972m * 19800m, line.AmountVnd);
    }

    [Fact]
    public void TwoLevels_SplitTheGoodsWeightAndDeductEachPartOnItsOwn()
    {
        // 16,020 kg of goods: 15,000 kg at 19,800 and the remaining 1,020 kg at 19,600.
        var lines = PriceBreakdown.Compute(16020m, 19800m, new PriceSplit(15000m, 19600m));

        Assert.Equal(2, lines.Count);
        Assert.Equal(new PriceLine(14955m, 19800m, 296_109_000m), lines[0]);   // 15,000 − 45
        Assert.Equal(new PriceLine(1017m, 19600m, 19_933_200m), lines[1]);     // 1,020 − 3.06 = 1,016.94
        Assert.Equal(316_042_200m, PriceBreakdown.TotalAmount(lines));
    }

    [Fact]
    public void MatchesTheOwnersSheet_ThaoVuTruckSplitIntoTwelveThousandAndTheRest()
    {
        // The sheet: 31,160 kg = 12,000 kg at 24,800 + 19,160 kg at 25,400, each part deducted separately.
        var lines = PriceBreakdown.Compute(31160m, 24800m, new PriceSplit(12000m, 25400m));

        Assert.Equal(11964m, lines[0].WeightKg);
        Assert.Equal(296_707_200m, lines[0].AmountVnd);
        Assert.Equal(19103m, lines[1].WeightKg); // 19,160 − 57.48 = 19,102.52
    }

    [Fact]
    public void MatchesTheOwnersSheet_LanhTruckFortyThousandPlusTheRest()
    {
        var lines = PriceBreakdown.Compute(45320m, 25900m, new PriceSplit(40000m, 25700m));

        Assert.Equal(new PriceLine(39880m, 25900m, 1_032_892_000m), lines[0]);
        Assert.Equal(5304m, lines[1].WeightKg);
    }

    [Theory]
    [InlineData(15000)]
    [InlineData(16020)]
    [InlineData(20000)]
    public void FirstPartCoveringTheWholeGoodsWeight_CollapsesToOneLine(int firstKg)
    {
        var lines = PriceBreakdown.Compute(15000m, 19800m, new PriceSplit(firstKg, 19600m));

        var line = Assert.Single(lines);
        Assert.Equal(19800m, line.UnitPriceVndPerKg);
        Assert.Equal(14955m, line.WeightKg);
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
    public void NoWeightOrNoPrice_HasNoLines()
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
        // 16,070 kg net: 15,000 kg (14,955 billable) at 19,800 + 1,070 kg (1,067 billable) at 19,600.
        var split = new PriceSplit(15000m, 19600m);

        var result = WeightCalculator.Calculate(5000m, 21070m, 19800m, split);

        Assert.Equal(16022m, result.BillableWeightKg);
        Assert.Equal(14955m * 19800m + 1067m * 19600m, result.TotalAmountVnd);
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
