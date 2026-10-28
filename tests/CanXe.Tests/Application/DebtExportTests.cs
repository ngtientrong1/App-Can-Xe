using CanXe.Debt.Contract;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Services;

namespace CanXe.Tests.Application;

public sealed class DebtExportTests
{
    private static readonly StationInfo Station = new("DAKHA", "Đắk Hà");

    private static WeighTicket Ticket(
        string code, string number, int weighings = 2, int? billableGrams = 16_022_000, int? price = 19_800,
        int? total = 317_031_200, bool deleted = false, int? splitFirstGrams = null, int? secondPrice = null)
    {
        var complete = weighings >= 2;
        var ticket = new WeighTicket
        {
            InternalCode = code,
            DisplayNumber = number,
            TicketDateTime = new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.FromHours(7)),
            CustomerNameSnapshot = "Công Ty Đại Lợi",
            LicensePlateSnapshot = "82C-072.19",
            CargoTypeNameSnapshot = "Rô Tươi",
            GrossWeightGrams = complete ? 21_070_000 : 21_070_000,
            TareWeightGrams = complete ? 5_000_000 : 0,
            NetWeightGrams = complete ? 16_070_000 : 21_070_000,
            BillableWeightGrams = billableGrams,
            UnitPriceVndPerKg = price,
            SplitFirstWeightGrams = splitFirstGrams,
            SecondUnitPriceVndPerKg = secondPrice,
            TotalAmountVnd = total,
            IsDeleted = deleted
        };
        for (var i = 1; i <= weighings; i++)
            ticket.Events.Add(new WeighEvent { Sequence = i, OriginalWeightGrams = i == 1 ? 21_070_000 : 5_000_000 });
        return ticket;
    }

    [Theory]
    [InlineData("Cân Dịch Vụ")]
    [InlineData("CAN DICH VU")]
    [InlineData("  cân   dịch vụ ")]
    public void ServiceWeighTicket_IsLeftOutOfTheDebtFile(string customer)
    {
        var service = Ticket("svc", "63/10", price: null, total: null, billableGrams: null);
        service.CustomerNameSnapshot = customer;

        var file = DebtExportService.BuildFile(Station, [Ticket("a", "1/10"), service], DateTimeOffset.Now);

        Assert.Equal("a", Assert.Single(file.Tickets).Code);
    }

    [Fact]
    public void CompletedTicket_IsExportedWithWeightsPriceAndAmount()
    {
        var file = DebtExportService.BuildFile(Station, [Ticket("202610-0001", "1/10")], DateTimeOffset.Now);

        var row = Assert.Single(file.Tickets);
        Assert.Equal("202610-0001", row.Code);
        Assert.Equal("Công Ty Đại Lợi", row.Customer);
        Assert.Equal(21_070m, row.GrossWeightKg);
        Assert.Equal(5_000m, row.TareWeightKg);
        Assert.Equal(16_070m, row.NetWeightKg);
        Assert.Equal(16_022m, row.BillableWeightKg);
        Assert.Equal(19_800m, row.UnitPrice);
        Assert.Equal(317_031_200L, row.AmountVnd);
        Assert.False(row.IsDeleted);
        Assert.False(row.AwaitingSecondWeigh);
        Assert.True(file.IsChecksumValid);
    }

    [Fact]
    public void TicketAwaitingItsSecondWeighing_IsExportedWithOnlyTheFirstWeight()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", weighings: 1, billableGrams: null, price: null, total: null)], DateTimeOffset.Now);

        var row = Assert.Single(file.Tickets);
        Assert.True(row.AwaitingSecondWeigh);
        Assert.Equal(21_070m, row.FirstWeightKg);
        Assert.Null(row.NetWeightKg);
        Assert.Null(row.GrossWeightKg);
        Assert.Null(row.AmountVnd);
    }

    [Fact]
    public void AwaitingTicketNeverCarriesAMoneyAmountEvenWithAPrice()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", weighings: 1, billableGrams: 21_000_000, price: 19_800, total: 415_800_000)], DateTimeOffset.Now);

        Assert.Null(Assert.Single(file.Tickets).AmountVnd);
    }

    [Fact]
    public void WeighedButUnpricedTicket_IsExportedWithItsNetWeightAndNoAmount()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", billableGrams: null, price: null, total: null)], DateTimeOffset.Now);

        var row = Assert.Single(file.Tickets);
        Assert.Equal(16_070m, row.NetWeightKg);
        Assert.Null(row.BillableWeightKg);
        Assert.Null(row.UnitPrice);
        Assert.Null(row.AmountVnd);
        Assert.False(row.AwaitingSecondWeigh);
    }

    [Fact]
    public void TicketWithNoWeighingAtAll_IsADraftAndIsLeftOut()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", weighings: 0, billableGrams: null, price: null, total: null)], DateTimeOffset.Now);

        Assert.Empty(file.Tickets);
    }

    [Fact]
    public void DeletedTicket_IsExportedFlaggedSoTheDebtAppDropsIt()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", deleted: true)], DateTimeOffset.Now);

        Assert.True(Assert.Single(file.Tickets).IsDeleted);
    }

    [Fact]
    public void ZeroPriceServiceWeighing_KeepsItsZeroAmount()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", price: 0, total: 0)], DateTimeOffset.Now);

        Assert.Equal(0L, Assert.Single(file.Tickets).AmountVnd);
    }

    [Fact]
    public void SplitPrice_IsCarriedOver()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", splitFirstGrams: 15_000_000, secondPrice: 19_600)], DateTimeOffset.Now);

        var row = Assert.Single(file.Tickets);
        Assert.Equal(15_000m, row.SplitFirstWeightKg);
        Assert.Equal(19_600m, row.SecondUnitPrice);
    }

    [Fact]
    public void ExportedFile_RoundTripsThroughTheSerializer()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("b", "2/10"), Ticket("a", "1/10")], new DateTimeOffset(2026, 10, 27, 18, 0, 0, TimeSpan.FromHours(7)));

        var parsed = DebtExportSerializer.Deserialize(DebtExportSerializer.Serialize(file));

        Assert.NotNull(parsed);
        Assert.True(parsed.IsChecksumValid);
        Assert.Equal(["a", "b"], parsed.Tickets.Select(t => t.Code));
        Assert.Equal("DAKHA", parsed.Station.Code);
    }

    [Theory]
    [InlineData("DAKHA", true)]
    [InlineData("dakto", true)]
    [InlineData("A", false)]
    [InlineData("DAK HA", false)]
    [InlineData("ĐẮK", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void StationCode_AcceptsOnlyLettersDigitsAndUnderscore(string? code, bool valid)
    {
        Assert.Equal(valid, DebtStationCodePolicy.IsValid(code));
    }
}
