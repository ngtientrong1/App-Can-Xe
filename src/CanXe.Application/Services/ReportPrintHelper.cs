using System.Globalization;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public static class ReportPrintHelper
{
    private static readonly CultureInfo Vn = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>The criteria that were set, e.g. "Khách hàng: Công Ty A  |  Loại hàng: Cà tươi".</summary>
    public static string DescribeCriteria(ReportFilter filter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.CustomerKeyword))
            parts.Add($"Khách hàng: {filter.CustomerKeyword.Trim()}");
        if (!string.IsNullOrWhiteSpace(filter.LicensePlateKeyword))
            parts.Add($"Biển số: {filter.LicensePlateKeyword.Trim()}");
        if (!string.IsNullOrWhiteSpace(filter.CargoTypeKeyword))
            parts.Add($"Loại hàng: {filter.CargoTypeKeyword.Trim()}");

        var state = filter.State switch
        {
            ReportWorkflowFilter.Completed => "Hoàn tất",
            ReportWorkflowFilter.AwaitingSecondWeigh => "Chờ cân lần 2",
            ReportWorkflowFilter.MissingPrice => "Chưa có giá",
            _ => null
        };
        if (state is not null)
            parts.Add($"Trạng thái: {state}");

        return parts.Count == 0 ? "Tất cả khách hàng, biển số, loại hàng" : string.Join("  |  ", parts);
    }

    public static string DescribePeriod(ReportFilter filter)
    {
        var from = filter.FromDate?.ToString("dd/MM/yyyy", Vn);
        var to = filter.ToDate?.ToString("dd/MM/yyyy", Vn);

        return (from, to) switch
        {
            (not null, not null) => $"Từ ngày {from} đến ngày {to}",
            (not null, null) => $"Từ ngày {from}",
            (null, not null) => $"Đến ngày {to}",
            _ => "Tất cả thời gian"
        };
    }

    /// <summary>Number of different vehicles (by normalized plate) in the rows.</summary>
    public static int CountDistinctVehicles(IEnumerable<ReportRowDto> rows) =>
        rows.Select(r => PlateNormalizer.Normalize(r.LicensePlate))
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Count();
}
