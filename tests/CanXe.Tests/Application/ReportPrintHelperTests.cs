using CanXe.Application.Models;
using CanXe.Application.Services;

namespace CanXe.Tests.Application;

public sealed class ReportPrintHelperTests
{
    [Fact]
    public void DescribeCriteria_ListsOnlyTheCriteriaThatWereSet()
    {
        var text = ReportPrintHelper.DescribeCriteria(new ReportFilter
        {
            CustomerKeyword = "  Công Ty Đại Lợi ",
            CargoTypeKeyword = "Cà tươi"
        });

        Assert.Equal("Khách hàng: Công Ty Đại Lợi  |  Loại hàng: Cà tươi", text);
    }

    [Fact]
    public void DescribeCriteria_NothingSet_SaysAll()
    {
        Assert.Equal(
            "Tất cả khách hàng, biển số, loại hàng",
            ReportPrintHelper.DescribeCriteria(new ReportFilter()));
    }

    [Fact]
    public void DescribeCriteria_IncludesNonDefaultStatus()
    {
        var text = ReportPrintHelper.DescribeCriteria(new ReportFilter { State = ReportWorkflowFilter.MissingPrice });

        Assert.Equal("Trạng thái: Chưa có giá", text);
    }

    [Fact]
    public void DescribePeriod_FormatsBothDates()
    {
        var text = ReportPrintHelper.DescribePeriod(new ReportFilter
        {
            FromDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            ToDate = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)
        });

        Assert.Equal("Từ ngày 01/10/2026 đến ngày 26/10/2026", text);
    }

    [Fact]
    public void CountDistinctVehicles_TreatsPlateFormattingAsTheSameVehicle()
    {
        var rows = new[]
        {
            new ReportRowDto { LicensePlate = "82C-072.19" },
            new ReportRowDto { LicensePlate = "82c07219" },
            new ReportRowDto { LicensePlate = "51D-999.99" },
            new ReportRowDto { LicensePlate = null }
        };

        Assert.Equal(2, ReportPrintHelper.CountDistinctVehicles(rows));
    }
}
