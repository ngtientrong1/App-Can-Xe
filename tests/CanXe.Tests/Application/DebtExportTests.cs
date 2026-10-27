using CanXe.Debt.Contract;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Services;

namespace CanXe.Tests.Application;

public sealed class DebtExportTests
{
    private static readonly StationInfo Station = new("DAKHA", "Đắk Hà");

    private static WeighTicket Ticket(
        string code, string number, int? billableGrams = 16_022_000, int? price = 19_800, int? total = 317_031_200,
        bool deleted = false, int? splitFirstGrams = null, int? secondPrice = null) =>
        new()
        {
            InternalCode = code,
            DisplayNumber = number,
            TicketDateTime = new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.FromHours(7)),
            CustomerNameSnapshot = "Công Ty Đại Lợi",
            LicensePlateSnapshot = "82C-072.19",
            CargoTypeNameSnapshot = "Rô Tươi",
            GrossWeightGrams = 21_070_000,
            TareWeightGrams = 5_000_000,
            NetWeightGrams = 16_070_000,
            BillableWeightGrams = billableGrams,
            UnitPriceVndPerKg = price,
            SplitFirstWeightGrams = splitFirstGrams,
            SecondUnitPriceVndPerKg = secondPrice,
            TotalAmountVnd = total,
            IsDeleted = deleted
        };

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
        Assert.True(file.IsChecksumValid);
    }

    [Fact]
    public void TicketAwaitingItsSecondWeighing_IsNotExported()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", billableGrams: null, price: null, total: null)], DateTimeOffset.Now);

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
    public void TicketWithoutPrice_HasNoAmount()
    {
        var file = DebtExportService.BuildFile(
            Station, [Ticket("a", "1/10", price: null, total: 0)], DateTimeOffset.Now);

        var row = Assert.Single(file.Tickets);
        Assert.Null(row.UnitPrice);
        Assert.Null(row.AmountVnd);
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
