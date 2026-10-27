using System.Globalization;
using CanXe.Application.Models;
using CanXe.Application.Services;

namespace CanXe.Tests.Application;

public sealed class PriceSplitDisplayTests
{
    private static string N(decimal value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static WeighTicketPrintModel Model(bool split, bool showPrice = true) =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "1/10",
                TicketDateTime = DateTimeOffset.Now,
                NetWeightKg = 16070m,
                BillableWeightKg = 16022m,
                UnitPriceVndPerKg = 19800m,
                SplitFirstWeightKg = split ? 15000m : null,
                SecondUnitPriceVndPerKg = split ? 19600m : null,
                TotalAmountVnd = 317_022_200m
            },
            new StationSettingsDto(),
            new PrintSettingsDto { ShowPrice = showPrice });

    [Fact]
    public void PrintedUnitPrice_ListsBothPricesForASplitTicket()
    {
        Assert.Equal($"{N(19800m)} / {N(19600m)}", WeighTicketPrintFormatter.FormatUnitPrice(Model(split: true)));
    }

    [Fact]
    public void PrintedUnitPrice_IsUnchangedForASingleTicket()
    {
        Assert.Equal($"{N(19800m)} VNĐ/kg", WeighTicketPrintFormatter.FormatUnitPrice(Model(split: false)));
    }

    [Fact]
    public void PrintedBreakdown_StatesEachPricedPart()
    {
        var text = WeighTicketPrintFormatter.FormatPriceBreakdown(Model(split: true));

        Assert.Equal($"{N(14955m)} kg × {N(19800m)}  +  {N(1067m)} kg × {N(19600m)}", text);
        Assert.Null(WeighTicketPrintFormatter.FormatPriceBreakdown(Model(split: false)));
    }

    [Fact]
    public void PricesAreHiddenWhenPrintSettingsHideThem()
    {
        var hidden = Model(split: true, showPrice: false);

        Assert.Null(WeighTicketPrintFormatter.FormatUnitPrice(hidden));
        Assert.Null(WeighTicketPrintFormatter.FormatPriceBreakdown(hidden));
    }

    [Fact]
    public void ReportRow_ShowsBothPricesOnlyWhenSplit()
    {
        var split = new ReportRowDto
        {
            UnitPriceVndPerKg = 19800m,
            SplitFirstWeightKg = 15000m,
            SecondUnitPriceVndPerKg = 19600m
        };
        var single = new ReportRowDto { UnitPriceVndPerKg = 19800m };
        var none = new ReportRowDto();

        Assert.True(split.HasPriceSplit);
        Assert.Equal($"{N(19800m)} / {N(19600m)}", split.UnitPriceDisplay);
        Assert.Equal(N(19800m), single.UnitPriceDisplay);
        Assert.Equal("—", none.UnitPriceDisplay);
    }
}
