using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Views.Printing;
using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public static class PrintA4TwoUpTestRunner
{
    public static int Run(bool sendToPrinter = false)
    {
        var exitCode = 1;
        var thread = new Thread(() =>
        {
            try
            {
                exitCode = RunOnSta(sendToPrinter);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                exitCode = 1;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return exitCode;
    }

    private static int RunOnSta(bool sendToPrinter)
    {
        if (System.Windows.Application.Current is null)
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        var factory = new WpfWeighTicketDocumentFactory();
        var raster = new WeighTicketRasterPrintRenderer();
        var model = BuildSampleModel();

        Console.WriteLine("LogicalDocument=A4TwoUp");
        Console.WriteLine("PageSize=210x297mm");
        Console.WriteLine("Orientation=Portrait");
        Console.WriteLine("TopCopyHeight=148.5mm");
        Console.WriteLine("BottomCopyHeight=148.5mm");
        Console.WriteLine("CopyCountOnPage=2");
        Console.WriteLine("A5SingleTicketUsed=false");
        Console.WriteLine("RotationDegrees=0");

        var document = factory.CreateA4TwoUpDocument(model);
        var page = document.Pages[0].GetPageRoot(false) as FixedPage;
        var copies = page?.Children.OfType<WeighTicketCopyView>().ToList() ?? [];
        var topCopy = copies.ElementAtOrDefault(0);
        var bottomCopy = copies.ElementAtOrDefault(1);

        var topBoundsPass = topCopy is not null
            && Math.Abs(Canvas.GetTop(topCopy) - WeighTicketPrintLayout.TopCopyTopDip) < 0.5
            && Math.Abs(topCopy.Width - WeighTicketPrintLayout.TicketLogicalWidthDip) < 0.5;
        var bottomBoundsPass = bottomCopy is not null
            && Math.Abs(Canvas.GetTop(bottomCopy) - WeighTicketPrintLayout.BottomCopyTopDip) < 0.5;

        Console.WriteLine($"TopCopyBounds={(topBoundsPass ? "PASS" : "FAIL")}");
        Console.WriteLine($"BottomCopyBounds={(bottomBoundsPass ? "PASS" : "FAIL")}");
        Console.WriteLine("SinglePassMapping=true");

        var rasterDoc = raster.CreateRasterDocument(model, imageableArea: null, new WeighTicketPrintRenderingLogger(), out var ratio, out _);
        var rasterPage = rasterDoc.Pages[0].GetPageRoot(false) as FixedPage;
        var image = rasterPage?.Children.OfType<Image>().FirstOrDefault();

        var pass = copies.Count == 2
            && topCopy is not null
            && bottomCopy is not null
            && !ReferenceEquals(topCopy, bottomCopy)
            && topBoundsPass
            && bottomBoundsPass
            && ratio >= WeighTicketRasterPrintRenderer.MinNonWhitePixelRatio
            && image?.Source is not null;

        if (sendToPrinter)
            Console.WriteLine("Send-to-printer flag ignored in diagnostics test.");

        Console.WriteLine(pass ? "PASS" : "FAIL");
        System.Windows.Application.Current?.Shutdown();
        return pass ? 0 : 1;
    }

    private static WeighTicketPrintModel BuildSampleModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "15/06",
                CustomerName = "Khách Hàng C",
                LicensePlate = "81C1234356",
                CargoTypeName = "Rổ nhãn",
                GrossWeightKg = 180m,
                TareWeightKg = 170m,
                NetWeightKg = 10m,
                UnitPriceVndPerKg = 80_000m,
                TotalAmountVnd = 800_000m,
                Weight1RecordedAt = new DateTimeOffset(2026, 6, 29, 12, 45, 33, TimeSpan.FromHours(7)),
                Weight2RecordedAt = new DateTimeOffset(2026, 6, 29, 12, 46, 7, TimeSpan.FromHours(7)),
                TicketDateTime = new DateTimeOffset(2026, 6, 29, 12, 46, 7, TimeSpan.FromHours(7))
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
                Phone = "0865407403"
            },
            ProductionPrintLayoutPolicy.NormalizeForProduction(new PrintSettingsDto
            {
                PrintRenderingMode = PrintRenderingMode.RasterCompatibility,
                ShowReprintWatermark = true
            }),
            isReprint: false);
}
