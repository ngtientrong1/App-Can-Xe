using CanXe.Domain.Services;

namespace CanXe.Tests.Domain;

public class WeightCalculatorTests
{
    [Fact]
    public void Calculate_With8500And18500Kg_MatchesExpectedFormula()
    {
        var result = WeightCalculator.Calculate(8500m, 18500m, 500m);

        Assert.Equal(18500m, result.GrossWeightKg);
        Assert.Equal(8500m, result.TareWeightKg);
        Assert.Equal(10000m, result.NetWeightKg);
        Assert.Equal(30m, result.DeductionWeightKg);
        Assert.Equal(9970m, result.BillableWeightKg);
        Assert.Equal(4_985_000m, result.TotalAmountVnd);
    }

    [Fact]
    public void Calculate_SingleWeight_UsesZeroTareAndSameNet()
    {
        var result = WeightCalculator.Calculate(11576m, null, null);

        Assert.Equal(11576m, result.GrossWeightKg);
        Assert.Equal(0m, result.TareWeightKg);
        Assert.Equal(11576m, result.NetWeightKg);
        Assert.Null(result.BillableWeightKg);
    }

    [Fact]
    public void Calculate_SingleWeightWithUnitPrice_ComputesBilling()
    {
        var result = WeightCalculator.Calculate(11576m, null, 500m);

        Assert.Equal(11576m, result.NetWeightKg);
        Assert.Equal(34.728m, result.DeductionWeightKg);
        Assert.Equal(11541m, result.BillableWeightKg);
        Assert.Equal(5_770_500m, result.TotalAmountVnd);
    }

    [Fact]
    public void Calculate_SingleWeightWithoutUnitPrice_BillingNull()
    {
        var result = WeightCalculator.Calculate(8500m, null, null);

        Assert.Equal(8500m, result.GrossWeightKg);
        Assert.Null(result.DeductionWeightKg);
        Assert.Null(result.TotalAmountVnd);
    }

    [Fact]
    public void Calculate_AfterSecondWeight_ReplacesSingleWeighResult()
    {
        var single = WeightCalculator.Calculate(11576m, null, null);
        Assert.Equal(0m, single.TareWeightKg);

        var dual = WeightCalculator.Calculate(8500m, 11576m, null);
        Assert.Equal(11576m, dual.GrossWeightKg);
        Assert.Equal(8500m, dual.TareWeightKg);
        Assert.Equal(3076m, dual.NetWeightKg);
    }

    [Fact]
    public void IsSingleWeigh_DetectsOneWeightOnly()
    {
        Assert.True(WeightCalculator.IsSingleWeigh(8500m, null));
        Assert.True(WeightCalculator.IsSingleWeigh(null, 9200m));
        Assert.False(WeightCalculator.IsSingleWeigh(8500m, 18500m));
    }

    [Fact]
    public void Calculate_WithoutUnitPrice_ComputesGrossTareNetButNotBilling()
    {
        var result = WeightCalculator.Calculate(8500m, 18500m, null);

        Assert.Equal(18500m, result.GrossWeightKg);
        Assert.Null(result.BillableWeightKg);
    }

    [Theory]
    [InlineData(10000.5, 10000.5)]
    [InlineData(10001.4, 10001.4)]
    public void Calculate_RoundsBillableWeightAwayFromZero(decimal w1, decimal w2)
    {
        var net = Math.Abs(w1 - w2);
        var deduction = net / 1000m * 3m;
        var raw = net - deduction;
        var expected = Math.Round(raw, 0, MidpointRounding.AwayFromZero);

        var result = WeightCalculator.Calculate(w1, w2, 500m);
        Assert.Equal(expected, result.BillableWeightKg);
    }
}
