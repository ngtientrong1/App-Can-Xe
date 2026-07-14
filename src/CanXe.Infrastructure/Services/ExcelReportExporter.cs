using CanXe.Application.Interfaces;

using CanXe.Application.Models;

using ClosedXML.Excel;



namespace CanXe.Infrastructure.Services;



public sealed class ExcelReportExporter : IExcelReportExporter

{

    private const string SheetName = "BaoCaoPhieuCan";

    private const string ThousandsFormat = "#,##0";



    public void Export(ReportResult report, ReportFilter filter, string filePath, string? stationName)

    {

        using var workbook = new XLWorkbook();

        var sheet = workbook.Worksheets.Add(SheetName);



        var row = 1;

        sheet.Cell(row, 1).Value = stationName ?? "Trạm cân CanXe";

        sheet.Cell(row, 1).Style.Font.Bold = true;

        sheet.Cell(row, 1).Style.Font.FontSize = 14;

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

        }



        row = headerRow + 1;

        var index = 1;

        foreach (var item in report.Rows)

        {

            sheet.Cell(row, 1).Value = index++;

            var dateCell = sheet.Cell(row, 2);

            dateCell.Value = ToExcelDateTime(item.TicketDateTime);

            dateCell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";

            sheet.Cell(row, 3).Value = item.DisplayNumber;

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



        sheet.Cell(row, 1).Value = "Tổng cộng";

        sheet.Cell(row, 1).Style.Font.Bold = true;

        WriteInteger(sheet.Cell(row, 3), report.Summary.TicketCount);

        WriteKg(sheet.Cell(row, 7), report.Summary.TotalGrossWeightKg);

        WriteKg(sheet.Cell(row, 8), report.Summary.TotalTareWeightKg);

        WriteKg(sheet.Cell(row, 9), report.Summary.TotalNetWeightKg);

        WriteKg(sheet.Cell(row, 10), report.Summary.TotalBillableWeightKg);

        WriteMoney(sheet.Cell(row, 12), report.Summary.TotalAmountVnd);

        sheet.Cell(row, 12).Style.Font.Bold = true;



        sheet.SheetView.FreezeRows(headerRow);

        sheet.Columns().AdjustToContents();

        workbook.SaveAs(filePath);

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

