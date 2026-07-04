using System.Printing;
using System.Windows;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Desktop.Views.Printing;

namespace CanXe.Desktop.Services;

public static class PrintA5FitTestRunner
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

        using var server = new LocalPrintServer();
        var queue = server.DefaultPrintQueue ?? server.GetPrintQueues().FirstOrDefault();
        if (queue is null)
        {
            Console.Error.WriteLine("ERROR: No printer available.");
            return 1;
        }

        var validated = A5PrintTicketResolver.MergeAndValidateA5Landscape(queue, copies: 1);
        var caps = PrinterCapabilitiesReader.ReadA5Print(
            queue,
            validated.ValidatedTicket,
            PageOrientation.Landscape);
        var model = BuildSampleModel();
        var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
        var landscapeBitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
        var placementBitmap = caps.Orientation.Mode == A5PhysicalOrientationMode.PortraitDriverFallbackRotateClockwise
            ? A5RasterBitmapRotator.RotateClockwise90(landscapeBitmap)
            : landscapeBitmap;
        var adaptive = PrintA5AdaptiveFitService.Resolve(
            placementBitmap,
            caps.Imageable,
            caps.PageWidthDip,
            caps.PageHeightDip,
            caps.Orientation.RotatedContentWidthDip,
            caps.Orientation.RotatedContentHeightDip,
            scanWatermark: false,
            caps.Orientation);

        Console.WriteLine($"RequestedOrientation=Landscape");
        Console.WriteLine($"ValidatedOrientation={validated.ValidatedOrientation}");
        Console.WriteLine($"ActualPageSize={caps.PageWidthDip:F2}x{caps.PageHeightDip:F2}");
        Console.WriteLine($"ActualPageAspect={caps.Orientation.ActualPageAspect}");
        Console.WriteLine($"DriverIgnoredLandscape={caps.Orientation.DriverIgnoredLandscape}");
        Console.WriteLine($"PhysicalOrientationMode={caps.Orientation.Mode}");
        Console.WriteLine($"RotationDegrees={caps.Orientation.RotationDegrees}");
        Console.WriteLine($"LogicalContentSize={WeighTicketPrintLayout.TicketLogicalWidthMm}x{WeighTicketPrintLayout.TicketLogicalHeightMm}");
        Console.WriteLine($"RotatedContentSize={caps.Orientation.RotatedContentWidthDip:F2}x{caps.Orientation.RotatedContentHeightDip:F2}");
        Console.WriteLine($"ImageableOrigin={caps.OriginWidthDip:F2},{caps.OriginHeightDip:F2}");
        Console.WriteLine($"ImageableExtent={caps.ExtentWidthDip:F2},{caps.ExtentHeightDip:F2}");
        Console.WriteLine($"FinalScale={adaptive.FinalFit.FinalScale:F4}");
        Console.WriteLine($"FinalPlacement={adaptive.FinalPlacement.LeftDip:F2},{adaptive.FinalPlacement.TopDip:F2},{adaptive.FinalPlacement.WidthDip:F2},{adaptive.FinalPlacement.HeightDip:F2}");
        var finalValidation = adaptive.AdjustedValidation ?? adaptive.InitialValidation;
        Console.WriteLine($"FinalClearance L={finalValidation.FinalClearanceLeftPx:F0} R={finalValidation.FinalClearanceRightPx:F0} T={finalValidation.FinalClearanceTopPx:F0} B={finalValidation.FinalClearanceBottomPx:F0}");
        Console.WriteLine($"ValidationResult={(adaptive.Passed ? "PASS" : "FAIL")}");
        Console.WriteLine($"SinglePassMapping={adaptive.FinalFit.SinglePassMapping}");

        var passed = adaptive.Passed && adaptive.FinalFit.FinalScale <= 1.0;
        Console.WriteLine(passed ? "PASS" : "FAIL");

        if (sendToPrinter)
        {
            var renderer = new WeighTicketRasterPrintRenderer();
            var logger = new WeighTicketPrintRenderingLogger();
            var document = renderer.CreateRasterDocument(model, caps, logger, out _, out _);
            var dialog = new System.Windows.Controls.PrintDialog
            {
                PrintQueue = queue,
                PrintTicket = validated.ValidatedTicket
            };
            dialog.PrintDocument(document.DocumentPaginator, "CanXe A5 fit test");
        }

        return passed ? 0 : 1;
    }

    private static WeighTicketPrintModel BuildSampleModel() =>
        PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
}
