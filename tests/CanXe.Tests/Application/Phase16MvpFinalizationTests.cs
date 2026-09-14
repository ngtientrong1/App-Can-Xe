using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Tests.Application;

public class FilterSummaryCalculatorMvpTests
{
    [Fact]
    public void Build_IncludesServiceWeighInTotalNet()
    {
        var items = new List<WeighTicketListItem>
        {
            new() { Id = 1, DisplayNumber = "0001/06", NetWeightKg = 5000m, UnitPriceVndPerKg = null },
            new() { Id = 2, DisplayNumber = "0002/06", NetWeightKg = 3000m, UnitPriceVndPerKg = 500m, BillableWeightKg = 2900m, TotalAmountVnd = 1450000m }
        };

        var result = FilterSummaryCalculator.Build(items);

        Assert.Equal(8000m, result.TotalNetWeightKg);
        Assert.Equal(2900m, result.TotalBillableWeightKg);
        Assert.Equal(1450000m, result.TotalAmountVnd);
        Assert.Equal(1, result.MissingPriceCount);
    }

    [Fact]
    public void Build_MissingPriceCount_OnlyTicketsWithNetButNoPrice()
    {
        var items = new List<WeighTicketListItem>
        {
            new() { Id = 1, DisplayNumber = "0001/06", NetWeightKg = 1000m },
            new() { Id = 2, DisplayNumber = "0002/06", NetWeightKg = 2000m, UnitPriceVndPerKg = 0m },
            new() { Id = 3, DisplayNumber = "0003/06", NetWeightKg = null }
        };

        var result = FilterSummaryCalculator.Build(items);
        Assert.Equal(2, result.MissingPriceCount);
    }
}

public class VehicleAutoFillTests
{
    [Fact]
    public void ApplyBoth_FillsCustomerAndRecentCargo()
    {
        var context = new VehicleUsageContext
        {
            VehicleId = 1,
            PlateNumber = "81C-04621",
            RecentCustomerId = 10,
            RecentCustomerName = "Chị Thanh",
            RecentCargoTypeId = 20,
            RecentCargoTypeName = "Rô tươi",
            FrequentCargoTypeId = 20,
            FrequentCargoTypeName = "Rô tươi"
        };

        var applied = VehicleUsageContextApplier.Apply(context, VehicleContextApplyMode.Both, null, null);

        Assert.Equal("Chị Thanh", applied.CustomerName);
        Assert.Equal("Rô tươi", applied.CargoTypeName);
    }

    [Fact]
    public void ApplyBoth_OnlyCustomerWhenNoCargoHistory()
    {
        var context = new VehicleUsageContext
        {
            VehicleId = 1,
            PlateNumber = "81C-04621",
            RecentCustomerId = 10,
            RecentCustomerName = "Chị Thanh"
        };

        var applied = VehicleUsageContextApplier.Apply(context, VehicleContextApplyMode.Both, null, null);

        Assert.Equal("Chị Thanh", applied.CustomerName);
        Assert.Null(applied.CargoTypeName);
    }
}

public class AutocompleteRankerMvpTests
{
    [Fact]
    public void Rank_TokenPrefixPrefersMatchingToken()
    {
        var items = new[] { "Anh Đức", "Chị Đào", "Chị Đức" };
        var ranked = AutocompleteRanker.Rank(
            items,
            x => x,
            _ => DateTimeOffset.Now,
            "Chị Đ",
            8);

        Assert.Equal("Chị Đức", ranked[0]);
        Assert.Equal("Chị Đào", ranked[1]);
    }
}

public sealed class FakeTicketDocumentRenderer : ITicketDocumentRenderer
{
    public TicketDocumentRenderResult RenderFront(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null) =>
        new() { PngBytes = [1, 2, 3], SuggestedFileName = "front.png" };

    public TicketDocumentRenderResult RenderBack(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null) =>
        new() { PngBytes = [4, 5, 6], SuggestedFileName = "back.png" };

    public TicketDocumentRenderResult RenderCombinedVertical(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null) =>
        new() { PngBytes = [1, 2, 3, 4, 5, 6], SuggestedFileName = "combined.png" };
}

public class TicketDocumentRendererContractTests
{
    [Fact]
    public void FakeRenderer_ReturnsCombinedPng()
    {
        var renderer = new FakeTicketDocumentRenderer();
        var detail = new WeighTicketDetailDto
        {
            Id = 1,
            DisplayNumber = "0023/06",
            TicketDateTime = DateTimeOffset.Now
        };

        var result = renderer.RenderCombinedVertical(detail);
        Assert.Equal(6, result.PngBytes.Length);
        Assert.Equal("combined.png", result.SuggestedFileName);
    }
}
