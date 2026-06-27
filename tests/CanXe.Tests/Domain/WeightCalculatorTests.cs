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
    public void Calculate_WithSingleWeight_ReturnsNullComputedFields()
    {
        var result = WeightCalculator.Calculate(8500m, null, 500m);

        Assert.Null(result.GrossWeightKg);
        Assert.Null(result.TareWeightKg);
        Assert.Null(result.NetWeightKg);
        Assert.Null(result.DeductionWeightKg);
        Assert.Null(result.BillableWeightKg);
        Assert.Null(result.TotalAmountVnd);
    }

    [Fact]
    public void Calculate_WithoutUnitPrice_ComputesGrossTareNetButNotBilling()
    {
        var result = WeightCalculator.Calculate(8500m, 18500m, null);

        Assert.Equal(18500m, result.GrossWeightKg);
        Assert.Equal(8500m, result.TareWeightKg);
        Assert.Equal(10000m, result.NetWeightKg);
        Assert.Null(result.DeductionWeightKg);
        Assert.Null(result.BillableWeightKg);
        Assert.Null(result.TotalAmountVnd);
    }

    [Fact]
    public void Calculate_WithZeroUnitPrice_TreatedAsServiceWeigh()
    {
        var result = WeightCalculator.Calculate(8500m, 18500m, 0m);

        Assert.Equal(10000m, result.NetWeightKg);
        Assert.Null(result.DeductionWeightKg);
        Assert.Null(result.BillableWeightKg);
        Assert.Null(result.TotalAmountVnd);
        Assert.False(WeightCalculator.HasBillableUnitPrice(0m));
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

    [Fact]
    public void Calculate_RemovingUnitPrice_ClearsBillingFields()
    {
        var withPrice = WeightCalculator.Calculate(8500m, 18500m, 500m);
        Assert.NotNull(withPrice.BillableWeightKg);

        var withoutPrice = WeightCalculator.Calculate(8500m, 18500m, null);
        Assert.Equal(withPrice.GrossWeightKg, withoutPrice.GrossWeightKg);
        Assert.Equal(withPrice.NetWeightKg, withoutPrice.NetWeightKg);
        Assert.Null(withoutPrice.DeductionWeightKg);
        Assert.Null(withoutPrice.BillableWeightKg);
        Assert.Null(withoutPrice.TotalAmountVnd);
    }
}
