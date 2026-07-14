using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IExcelReportExporter
{
    void Export(ReportResult report, ReportFilter filter, string filePath, string? stationName);
}
