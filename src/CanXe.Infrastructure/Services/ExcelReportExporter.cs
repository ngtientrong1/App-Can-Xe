using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using ClosedXML.Excel;

namespace CanXe.Infrastructure.Services;

public sealed class ExcelReportExporter : IExcelReportExporter
{
    private const string SheetName = "BaoCaoPhieuCan";
    private const string ThousandsFormat = "#,##0";
    private const int ColumnCount = 13;

    public void Export(ReportResult report, ReportFilter filter, string filePath, string? stationName)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);

        var row = 1;
        var titleCell = sheet.Cell(row, 1);
        titleCell.Value = stationName ?? "Trạm cân CanXe";
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 14;
        row++;

        var fromText = filter.FromDate?.ToString("dd/MM/yyyy") ?? "—";
        var toText = filter.ToDate?.ToString("dd/MM/yyyy") ?? "—";
        sheet.Cell(row, 1).Value = $"Khoảng ngày: {fromText} — {toText}";
        row++;
        sheet.Cell(row, 1).Value = $"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}";
        row += 2;

        var headerRow = row;
        var headers = new[]
        {
            "STT", "Ngày giờ", "Số phiếu", "Khách hàng", "Biển số", "Loại hàng",
            "Tổng", "Bì", "Hàng", "KL tính tiền", "Đơn giá", "Thành tiền", "Ghi chú"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = sheet.Cell(headerRow, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");
            cell.Style.Alignment.Horizontal = col is 0 or 2
                ? XLAlignmentHorizontalValues.Center
                : XLAlignmentHorizontalValues.Left;
        }

        // Excel-only: oldest → newest (UI report may stay newest-first).
        var orderedRows = report.Rows
            .OrderBy(r => r.TicketDateTime)
            .ThenBy(r => r.DisplayNumber, StringComparer.Ordinal)
            .ThenBy(r => r.TicketId)
            .ToList();

        row = headerRow + 1;
        var index = 1;
        foreach (var item in orderedRows)
        {
            var sttCell = sheet.Cell(row, 1);
            sttCell.Value = index++;
            sttCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var dateCell = sheet.Cell(row, 2);
            dateCell.Value = ToExcelDateTime(item.TicketDateTime);
            dateCell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";

            var numberCell = sheet.Cell(row, 3);
            numberCell.Value = item.DisplayNumber;
            numberCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            sheet.Cell(row, 4).Value = item.CustomerName ?? string.Empty;
            sheet.Cell(row, 5).Value = item.LicensePlate ?? string.Empty;
            sheet.Cell(row, 6).Value = item.CargoTypeName ?? string.Empty;
            WriteKg(sheet.Cell(row, 7), item.GrossWeightKg);
            WriteKg(sheet.Cell(row, 8), item.TareWeightKg);
            WriteKg(sheet.Cell(row, 9), item.NetWeightKg);
            WriteKg(sheet.Cell(row, 10), item.BillableWeightKg);
            WriteMoney(sheet.Cell(row, 11), item.UnitPriceVndPerKg);
            WriteMoney(sheet.Cell(row, 12), item.TotalAmountVnd);
            sheet.Cell(row, 13).Value = item.Notes ?? string.Empty;
            row++;
        }

        var totalsRow = row;
        var totalsLabel = sheet.Cell(totalsRow, 1);
        totalsLabel.Value = "Tổng cộng";
        totalsLabel.Style.Font.Bold = true;

        WriteInteger(sheet.Cell(totalsRow, 3), report.Summary.TicketCount);
        WriteKg(sheet.Cell(totalsRow, 7), report.Summary.TotalGrossWeightKg);
        WriteKg(sheet.Cell(totalsRow, 8), report.Summary.TotalTareWeightKg);
        WriteKg(sheet.Cell(totalsRow, 9), report.Summary.TotalNetWeightKg);
        WriteKg(sheet.Cell(totalsRow, 10), report.Summary.TotalBillableWeightKg);
        WriteMoney(sheet.Cell(totalsRow, 12), report.Summary.TotalAmountVnd);

        for (var col = 1; col <= ColumnCount; col++)
        {
            var cell = sheet.Cell(totalsRow, col);
            cell.Style.Font.Bold = true;
            cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorderColor = XLColor.FromHtml("#334155");
        }

        var tableEndRow = Math.Max(headerRow, totalsRow - 1);
        if (orderedRows.Count > 0)
            tableEndRow = totalsRow - 1;

        var tableRange = sheet.Range(headerRow, 1, Math.Max(headerRow, tableEndRow), ColumnCount);
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        tableRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");

        // Include totals in outer border for the full report block.
        var fullRange = sheet.Range(headerRow, 1, totalsRow, ColumnCount);
        fullRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        fullRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94A3B8");

        sheet.Range(headerRow, 1, Math.Max(headerRow, tableEndRow), ColumnCount).SetAutoFilter();
        sheet.SheetView.FreezeRows(headerRow);
        ApplyColumnWidths(sheet);

        workbook.SaveAs(filePath);
    }

    private static void ApplyColumnWidths(IXLWorksheet sheet)
    {
        sheet.Column(1).Width = 6;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 12;
        sheet.Column(4).Width = 22;
        sheet.Column(5).Width = 14;
        sheet.Column(6).Width = 14;
        sheet.Column(7).Width = 12;
        sheet.Column(8).Width = 12;
        sheet.Column(9).Width = 12;
        sheet.Column(10).Width = 14;
        sheet.Column(11).Width = 12;
        sheet.Column(12).Width = 14;
        sheet.Column(13).Width = 24;
    }

    private static DateTime ToExcelDateTime(DateTimeOffset value)
    {
        var local = value.ToLocalTime().DateTime;
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    private static void WriteKg(IXLCell cell, decimal? value)
    {
        if (value is null)
            return;

        WriteNumeric(cell, value.Value);
    }

    private static void WriteKg(IXLCell cell, decimal value) => WriteNumeric(cell, value);

    private static void WriteMoney(IXLCell cell, decimal? value)
    {
        if (value is null)
            return;

        WriteNumeric(cell, value.Value);
    }

    private static void WriteInteger(IXLCell cell, int value) => WriteNumeric(cell, value);

    private static void WriteNumeric(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = ThousandsFormat;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }
}
