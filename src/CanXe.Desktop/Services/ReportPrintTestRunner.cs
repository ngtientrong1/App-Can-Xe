using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanXe.Application.Models;
using CanXe.Application.Services;

namespace CanXe.Desktop.Services;

/// <summary>Renders a sample report to PNG files so the printed layout can be checked without a printer.</summary>
public static class ReportPrintTestRunner
{
    public static int Run(string outputDirectory, int rowCount, bool portrait, bool multiCustomer = false)
    {
        var exitCode = 1;
        var thread = new Thread(() =>
        {
            try
            {
                exitCode = RunOnSta(outputDirectory, rowCount, portrait, multiCustomer);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex}");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return exitCode;
    }

    private static int RunOnSta(string outputDirectory, int rowCount, bool portrait, bool multiCustomer)
    {
        if (System.Windows.Application.Current is null)
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        Directory.CreateDirectory(outputDirectory);

        var plates = new[] { "82C-072.19", "51D-999.99", "82C-123.45" };
        var rows = Enumerable.Range(1, rowCount).Select(i => new ReportRowDto
        {
            TicketDateTime = new DateTimeOffset(2026, 10, 1 + (i % 25), 7 + (i % 10), i % 60, 0, TimeSpan.FromHours(7)),
            DisplayNumber = $"{i}/10",
            CustomerName = multiCustomer && i % 2 == 0 ? "Cty Lê Toàn Lộc" : "Công Ty Đại Lợi",
            LicensePlate = plates[i % plates.Length],
            CargoTypeName = "Cà tươi",
            GrossWeightKg = 21390 + i * 10,
            TareWeightKg = 11570,
            NetWeightKg = 9820 + i * 10,
            BillableWeightKg = 9820 + i * 10,
            UnitPriceVndPerKg = i % 7 == 0 ? null : 20000,
            TotalAmountVnd = i % 7 == 0 ? null : (9820 + i * 10) * 20000m
        }).ToList();

        var summary = new ReportSummaryDto
        {
            TicketCount = rows.Count,
            TotalNetWeightKg = rows.Sum(r => r.NetWeightKg ?? 0),
            TotalBillableWeightKg = rows.Sum(r => r.BillableWeightKg ?? 0),
            TotalAmountVnd = rows.Sum(r => r.TotalAmountVnd ?? 0),
            MissingPriceCount = rows.Count(r => r.UnitPriceVndPerKg is null)
        };

        var filter = new ReportFilter
        {
            FromDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            ToDate = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
            CustomerKeyword = "Công Ty Đại Lợi"
        };

        var model = new ReportPrintModel
        {
            Station = new StationSettingsDto
            {
                StationName = "Trạm cân Tiến Trọng",
                Address = "Quốc lộ 1A, Khánh Hòa",
                Phone = "0900 000 000"
            },
            Period = ReportPrintHelper.DescribePeriod(filter),
            Criteria = ReportPrintHelper.DescribeCriteria(filter),
            Rows = rows,
            Summary = summary,
            DistinctVehicles = ReportPrintHelper.CountDistinctVehicles(rows)
        };

        // Designed on a landscape A4 (96 dpi); with portrait the sheet is the same page turned 90 degrees.
        const double designWidth = 1122.5, designHeight = 793.7;
        var document = ReportPrintDocumentBuilder.Build(model, designWidth, designHeight, rotateToPortrait: portrait);
        var (width, height) = portrait ? (designHeight, designWidth) : (designWidth, designHeight);
        Console.WriteLine($"Rows={rows.Count} Pages={document.Pages.Count} Sheet={width:F0}x{height:F0}");

        for (var i = 0; i < document.Pages.Count; i++)
        {
            var page = document.Pages[i].GetPageRoot(false);
            var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(page);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(outputDirectory, $"report-page-{i + 1}.png");
            using var stream = File.Create(path);
            encoder.Save(stream);
            Console.WriteLine(path);
        }

        return document.Pages.Count > 0 ? 0 : 1;
    }
}
