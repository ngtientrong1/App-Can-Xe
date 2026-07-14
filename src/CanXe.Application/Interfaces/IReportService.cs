using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IReportService
{
    Task<ReportResult> QueryAsync(ReportFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResult> ExportExcelAsync(ReportFilter filter, string filePath, string? stationName, CancellationToken cancellationToken = default);
}
