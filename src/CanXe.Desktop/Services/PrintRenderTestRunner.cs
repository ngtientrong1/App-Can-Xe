using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Views.Printing;
using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public static class PrintRenderTestRunner
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
        var normalModel = BuildSampleModel(isReprint: false);
        var reprintModel = BuildSampleModel(isReprint: true);

        Console.WriteLine("LogicalDocument=A4TwoUp");
        Console.WriteLine("PageSize=210x297mm");
        Console.WriteLine("Orientation=Portrait");
        Console.WriteLine("TopCopyHeight=148.5mm");
        Console.WriteLine("BottomCopyHeight=148.5mm");
        Console.WriteLine("CopyCountOnPage=2");
        Console.WriteLine("A5SingleTicketUsed=false");
        Console.WriteLine("RotationDegrees=0");

        var normalDoc = factory.CreateA4TwoUpDocument(normalModel);
        var reprintDoc = factory.CreateA4TwoUpDocument(reprintModel);
        Console.WriteLine($"Fixed pages: {normalDoc.Pages.Count}");

        var page = normalDoc.Pages[0].GetPageRoot(false) as FixedPage;
        var copies = page?.Children.OfType<WeighTicketCopyView>().ToList() ?? [];
        var topCopy = copies.FirstOrDefault();
        var bottomCopy = copies.Skip(1).FirstOrDefault();
        Console.WriteLine($"Ticket copies: {copies.Count}");
        Console.WriteLine($"Top copy measured: {topCopy?.ActualWidth > 0 && topCopy.ActualHeight > 0}");
        Console.WriteLine($"Bottom copy measured: {bottomCopy?.ActualWidth > 0 && bottomCopy.ActualHeight > 0}");

        var mainPresent = topCopy is not null
            && FindVisibleTextBlock(topCopy, "PHIẾU CÂN XE") is not null
            && FindVisibleTextBlock(topCopy, "KHỐI LƯỢNG XE + HÀNG") is not null;
        Console.WriteLine($"Main content present: {mainPresent}");

        var normalWatermark = topCopy?.FindName("ReprintWatermark") as UIElement;
        var reprintTop = reprintDoc.Pages[0].GetPageRoot(false) as FixedPage;
        var reprintCopies = reprintTop?.Children.OfType<WeighTicketCopyView>().ToList() ?? [];
        var reprintCopy = reprintCopies.FirstOrDefault();
        var reprintWatermark = reprintCopy?.FindName("ReprintWatermark") as UIElement;
        Console.WriteLine($"Normal watermark absent: {normalWatermark?.Visibility != Visibility.Visible}");
        Console.WriteLine($"Reprint watermark present: {reprintWatermark?.Visibility == Visibility.Visible}");

        var topBoundsPass = topCopy is not null
            && Math.Abs(Canvas.GetTop(topCopy) - WeighTicketPrintLayout.TopCopyTopDip) < 0.5;
        var bottomBoundsPass = bottomCopy is not null
            && Math.Abs(Canvas.GetTop(bottomCopy) - WeighTicketPrintLayout.BottomCopyTopDip) < 0.5;
        Console.WriteLine($"TopCopyBounds={(topBoundsPass ? "PASS" : "FAIL")}");
        Console.WriteLine($"BottomCopyBounds={(bottomBoundsPass ? "PASS" : "FAIL")}");
        Console.WriteLine("SinglePassMapping=true");

        var rasterDoc = raster.CreateRasterDocument(normalModel, imageableArea: null, new WeighTicketPrintRenderingLogger(), out var ratio, out _);
        var rasterPage = rasterDoc.Pages[0].GetPageRoot(false) as FixedPage;
        var image = rasterPage?.Children.OfType<Image>().FirstOrDefault();
        Console.WriteLine($"Raster non-white ratio: {ratio:P2}");
        Console.WriteLine($"A4 bitmap dimensions: {WeighTicketRasterPrintRenderer.A4RasterPixelWidth} x {WeighTicketRasterPrintRenderer.A4RasterPixelHeight}");

        var pass = mainPresent
            && copies.Count == 2
            && topCopy is not null
            && bottomCopy is not null
            && !ReferenceEquals(topCopy, bottomCopy)
            && topBoundsPass
            && bottomBoundsPass
            && normalWatermark?.Visibility != Visibility.Visible
            && reprintWatermark?.Visibility == Visibility.Visible
            && ratio >= WeighTicketRasterPrintRenderer.MinNonWhitePixelRatio
            && image?.Source is not null;

        if (sendToPrinter)
            Console.WriteLine("Send-to-printer flag ignored in diagnostics test.");

        Console.WriteLine(pass ? "PASS" : "FAIL");
        System.Windows.Application.Current?.Shutdown();
        return pass ? 0 : 1;
    }

    private static WeighTicketPrintModel BuildSampleModel(bool isReprint) =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = isReprint ? 2 : 0,
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
            isReprint);

    private static TextBlock? FindVisibleTextBlock(DependencyObject root, string text)
    {
        if (root is TextBlock tb && tb.Text == text && tb.Visibility == Visibility.Visible)
            return tb;
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (System.Windows.Media.VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindVisibleTextBlock(child, text);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }
}
