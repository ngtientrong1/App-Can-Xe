using System.IO;
using System.Printing;
using System.Windows;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class WeighTicketPrintRenderingLogger
{
    private static string LogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanXe", "Logs", "print-rendering.log");

    public void LogJobStarted(WeighTicketPrintModel model, string printerName, string? driverName, PrintRenderingMode mode)
    {
        Write([
            $"[{DateTimeOffset.Now:O}] Print job started",
            $"Print job ID: pending",
            $"Printer: {printerName}",
            $"Driver: {driverName ?? "unknown"}",
            $"Rendering mode: {mode}",
            $"Ticket: {model.DisplayNumber}",
            $"IsReprint: {model.IsReprint}",
        ]);
    }

    public void LogDocumentBuilt(
        WeighTicketPrintModel model,
        int fixedPageCount,
        int topChildCount,
        int bottomChildCount,
        Size topCopySize,
        Size bottomCopySize,
        bool topDataContextPresent,
        bool bottomDataContextPresent,
        int topDescendants,
        int bottomDescendants)
    {
        Write([
            $"[{DateTimeOffset.Now:O}] Document built",
            $"Ticket: {model.DisplayNumber}",
            $"Fixed pages: {fixedPageCount}",
            $"Top copy ActualWidth/Height: {topCopySize.Width:F2} x {topCopySize.Height:F2}",
            $"Bottom copy ActualWidth/Height: {bottomCopySize.Width:F2} x {bottomCopySize.Height:F2}",
            $"Top DataContext present: {topDataContextPresent}",
            $"Bottom DataContext present: {bottomDataContextPresent}",
            $"Top visual descendants: {topDescendants}",
            $"Bottom visual descendants: {bottomDescendants}",
            $"Top copy children on page: {topChildCount}",
            $"Bottom copy children on page: {bottomChildCount}",
        ]);
    }

    public void LogPaginator(
        PageImageableArea? imageable,
        double scaleFactor,
        string writeState)
    {
        var lines = new List<string>
        {
            $"[{DateTimeOffset.Now:O}] Paginator",
            $"Page size DIP: {WeighTicketPrintLayout.PageWidthDip:F2} x {WeighTicketPrintLayout.PageHeightDip:F2}",
            $"Final scale: {scaleFactor:F4}",
            $"Write: {writeState}",
        };

        if (imageable is not null)
        {
            lines.Add($"Imageable origin: {imageable.OriginWidth:F2} x {imageable.OriginHeight:F2}");
            lines.Add($"Imageable extent: {imageable.ExtentWidth:F2} x {imageable.ExtentHeight:F2}");
        }

        Write(lines);
    }

    public void LogRaster(string stage, int width, int height, double nonWhiteRatio)
    {
        Write([
            $"[{DateTimeOffset.Now:O}] Raster {stage}",
            $"A4 bitmap: {width} x {height}",
            $"Non-white pixel ratio: {nonWhiteRatio:P2}",
        ]);
    }

    public void LogRasterImageableMapping(
        double rawOriginX,
        double rawOriginY,
        double rawExtentW,
        double rawExtentH,
        RasterPlacement placement)
    {
        Write([
            $"[{DateTimeOffset.Now:O}] Raster imageable mapping",
            $"Raw imageable origin DIP: {rawOriginX:F2} x {rawOriginY:F2}",
            $"Raw imageable extent DIP: {rawExtentW:F2} x {rawExtentH:F2}",
            $"Safety inset H/V mm: {WeighTicketPrintLayout.PrinterImageableSafetyInsetHorizontalMm} / {WeighTicketPrintLayout.PrinterImageableSafetyInsetVerticalMm}",
            $"Target rect DIP: {placement.TargetLeftDip:F2} x {placement.TargetTopDip:F2} {placement.TargetWidthDip:F2} x {placement.TargetHeightDip:F2}",
            $"Final scale: {placement.ScaleFactor:F4}",
            $"Final translate DIP: {placement.LeftDip:F2} x {placement.TopDip:F2}",
            $"Final image DIP: {placement.WidthDip:F2} x {placement.HeightDip:F2}",
            $"SinglePassMapping: {placement.SinglePassMapping}",
        ]);
    }

    [Obsolete("Use RasterPlacement overload.")]
    public void LogRasterImageableMapping(double originX, double originY, double extentW, double extentH, bool singlePass)
    {
        Write([
            $"[{DateTimeOffset.Now:O}] Raster imageable mapping",
            $"Origin DIP: {originX:F2} x {originY:F2}",
            $"Extent DIP: {extentW:F2} x {extentH:F2}",
            $"Single-pass mapping: {singlePass}",
        ]);
    }

    public void LogRasterClearance(PrintLayoutGeometryReport report)
    {
        Write(report.Lines.Prepend($"[{DateTimeOffset.Now:O}] Raster edge clearance").ToArray());
    }

    public void LogA5Fit(string stage, IEnumerable<string> lines)
    {
        Write(lines.Prepend($"[{DateTimeOffset.Now:O}] A5 fit {stage}").ToArray());
    }

    public void LogError(string message) =>
        Write([$"[{DateTimeOffset.Now:O}] ERROR: {message}"]);

    private static void Write(IEnumerable<string> lines)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllLines(LogPath, lines);
            File.AppendAllText(LogPath, Environment.NewLine);
        }
        catch
        {
            // diagnostics only
        }
    }
}
