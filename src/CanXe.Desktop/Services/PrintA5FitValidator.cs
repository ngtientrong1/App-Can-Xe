using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class PrintA5FitReport
{
    public bool Passed { get; init; }
    public IReadOnlyList<string> Lines { get; init; } = [];
    public double HeaderLeftInsetMm { get; init; }
    public double HeaderTopInsetMm { get; init; }
    public double HeaderPhoneBottomClearanceMm { get; init; }
    public double HeaderRightInsetMm { get; init; }
    public double LeftEdgeClearancePx { get; init; }
    public double RightEdgeClearancePx { get; init; }
    public double TopEdgeClearancePx { get; init; }
    public double BottomEdgeClearancePx { get; init; }
}

public static class PrintA5FitValidator
{
    public static PrintA5FitReport ValidateHeaderTextBounds(WeighTicketCopyView copy)
    {
        WpfWeighTicketDocumentFactory.MaterializeTicket(copy);
        var lines = new List<string>();
        var failures = 0;

        var safeHost = copy.FindName("SafeContentHost") as FrameworkElement;
        var header = copy.FindName("HeaderSection") as FrameworkElement;
        var stationName = copy.FindName("StationNameText") as FrameworkElement;
        var stationPhone = copy.FindName("StationPhoneText") as FrameworkElement;
        var stationType = copy.FindName("StationTypeText") as FrameworkElement;
        var ticketNumber = copy.FindName("TicketNumberText") as FrameworkElement;

        if (safeHost is null || header is null || stationName is null || stationPhone is null)
        {
            return new PrintA5FitReport
            {
                Passed = false,
                Lines = ["FAIL: Missing named header elements for bounds gate."]
            };
        }

        var safeBounds = GetBounds(copy, safeHost);
        var headerBounds = GetBounds(copy, header);
        var nameBounds = GetBounds(copy, stationName);
        var phoneBounds = GetBounds(copy, stationPhone);

        var leftInsetMm = WeighTicketPrintLayout.DipToMm(nameBounds.Left - safeBounds.Left);
        var phoneLeftInsetMm = WeighTicketPrintLayout.DipToMm(phoneBounds.Left - safeBounds.Left);
        var topInsetMm = WeighTicketPrintLayout.DipToMm(nameBounds.Top - safeBounds.Top);
        var phoneBottomClearanceMm = WeighTicketPrintLayout.DipToMm(headerBounds.Bottom - phoneBounds.Bottom);

        lines.Add($"HeaderLeftInset={leftInsetMm:F2}mm");
        lines.Add($"HeaderPhoneLeftInset={phoneLeftInsetMm:F2}mm");
        lines.Add($"HeaderTopInset={topInsetMm:F2}mm");
        lines.Add($"HeaderPhoneBottomClearance={phoneBottomClearanceMm:F2}mm");

        if (leftInsetMm < WeighTicketPrintLayout.HeaderLeftInsetLeftMm - 0.1)
        {
            failures++;
            lines.Add($"FAIL: Station name left inset {leftInsetMm:F2}mm < {WeighTicketPrintLayout.HeaderLeftInsetLeftMm}mm");
        }

        if (phoneLeftInsetMm < WeighTicketPrintLayout.HeaderLeftInsetLeftMm - 0.1)
        {
            failures++;
            lines.Add($"FAIL: Phone left inset {phoneLeftInsetMm:F2}mm < {WeighTicketPrintLayout.HeaderLeftInsetLeftMm}mm");
        }

        if (topInsetMm < WeighTicketPrintLayout.HeaderLeftInsetTopMm - 0.1)
        {
            failures++;
            lines.Add($"FAIL: Header top inset {topInsetMm:F2}mm < {WeighTicketPrintLayout.HeaderLeftInsetTopMm}mm");
        }

        if (phoneBottomClearanceMm < WeighTicketPrintLayout.HeaderLeftInsetBottomMm - 0.1)
        {
            failures++;
            lines.Add($"FAIL: Phone bottom clearance {phoneBottomClearanceMm:F2}mm < {WeighTicketPrintLayout.HeaderLeftInsetBottomMm}mm");
        }

        var headerRightInsetMm = 0.0;
        if (stationType is not null)
        {
            var typeBounds = GetBounds(copy, stationType);
            headerRightInsetMm = WeighTicketPrintLayout.DipToMm(safeBounds.Right - typeBounds.Right);
            lines.Add($"HeaderRightInset={headerRightInsetMm:F2}mm");
            if (headerRightInsetMm < WeighTicketPrintLayout.HeaderRightMinInsetFromSafeRightMm - 0.1)
            {
                failures++;
                lines.Add($"FAIL: Header right inset {headerRightInsetMm:F2}mm < {WeighTicketPrintLayout.HeaderRightMinInsetFromSafeRightMm}mm");
            }
        }

        if (ticketNumber is not null)
        {
            var ticketBounds = GetBounds(copy, ticketNumber);
            if (ticketBounds.Right > safeBounds.Right + WeighTicketPrintLayout.GeometryToleranceDip)
            {
                failures++;
                lines.Add("FAIL: Ticket number exceeds safe content right edge.");
            }
        }

        foreach (var element in new FrameworkElement?[] { stationName, stationPhone, stationType, ticketNumber })
        {
            if (element is null)
                continue;

            if (HasClippingAncestor(element))
            {
                failures++;
                lines.Add($"FAIL: {element.Name} has clipping ancestor.");
            }
        }

        lines.Add(failures == 0 ? "PASS" : "FAIL");
        return new PrintA5FitReport
        {
            Passed = failures == 0,
            Lines = lines,
            HeaderLeftInsetMm = leftInsetMm,
            HeaderTopInsetMm = topInsetMm,
            HeaderPhoneBottomClearanceMm = phoneBottomClearanceMm,
            HeaderRightInsetMm = headerRightInsetMm
        };
    }

    public static PrintA5FitReport ValidateA5PageRasterPlacement(BitmapSource bitmap, RasterPlacement placement) =>
        ValidateA5PageRasterPlacement(
            bitmap,
            placement,
            WeighTicketPrintLayout.A5LandscapePageWidthDip,
            WeighTicketPrintLayout.A5LandscapePageHeightDip);

    public static PrintA5FitReport ValidateA5PageRasterPlacement(
        BitmapSource bitmap,
        RasterPlacement placement,
        double pageWidthDip,
        double pageHeightDip)
    {
        var lines = new List<string>();
        var analysis = RasterEdgeClearanceAnalyzer.AnalyzeA5Page(bitmap, placement, pageWidthDip, pageHeightDip);
        lines.Add($"LeftEdgeClearance={analysis.LeftClearancePx:F0}px");
        lines.Add($"RightEdgeClearance={analysis.RightClearancePx:F0}px");
        lines.Add($"TopEdgeClearance={analysis.TopClearancePx:F0}px");
        lines.Add($"BottomEdgeClearance={analysis.BottomClearancePx:F0}px");

        var failures = 0;
        if (analysis.LeftClearancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Left clearance {analysis.LeftClearancePx:F0}px");
        }

        if (analysis.RightClearancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Right clearance {analysis.RightClearancePx:F0}px");
        }

        if (analysis.TopClearancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Top clearance {analysis.TopClearancePx:F0}px");
        }

        if (analysis.BottomClearancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Bottom clearance {analysis.BottomClearancePx:F0}px");
        }

        lines.Add(failures == 0 ? "PASS" : "FAIL");
        return new PrintA5FitReport
        {
            Passed = failures == 0,
            Lines = lines,
            LeftEdgeClearancePx = analysis.LeftClearancePx,
            RightEdgeClearancePx = analysis.RightClearancePx,
            TopEdgeClearancePx = analysis.TopClearancePx,
            BottomEdgeClearancePx = analysis.BottomClearancePx
        };
    }

    public static PrintA5FitReport ValidateRasterPlacement(BitmapSource bitmap, RasterPlacement placement)
    {
        var lines = new List<string>();
        var analysis = RasterEdgeClearanceAnalyzer.AnalyzeA5Ticket(bitmap, placement);
        lines.Add($"LeftEdgeClearance={analysis.LeftClearancePx:F0}px");
        lines.Add($"RightEdgeClearance={analysis.RightClearancePx:F0}px");
        lines.Add($"TopEdgeClearance={analysis.TopClearancePx:F0}px");
        lines.Add($"BottomEdgeClearance={analysis.BottomClearancePx:F0}px");

        var failures = 0;
        if (analysis.LeftClearancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Left clearance {analysis.LeftClearancePx:F0}px");
        }

        if (analysis.RightClearancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Right clearance {analysis.RightClearancePx:F0}px");
        }

        if (analysis.TopClearancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Top clearance {analysis.TopClearancePx:F0}px");
        }

        if (analysis.BottomClearancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Bottom clearance {analysis.BottomClearancePx:F0}px");
        }

        lines.Add(failures == 0 ? "PASS" : "FAIL");
        return new PrintA5FitReport
        {
            Passed = failures == 0,
            Lines = lines,
            LeftEdgeClearancePx = analysis.LeftClearancePx,
            RightEdgeClearancePx = analysis.RightClearancePx,
            TopEdgeClearancePx = analysis.TopClearancePx,
            BottomEdgeClearancePx = analysis.BottomClearancePx
        };
    }

    public static void EnsurePlacementValid(PrintA5FitResult fit)
    {
        if (fit.TargetWidthDip <= 0 || fit.TargetHeightDip <= 0)
            throw new InvalidOperationException("Vùng in an toàn của máy in không hợp lệ.");
    }

    private static Rect GetBounds(Visual root, FrameworkElement element)
    {
        var topLeft = element.TransformToVisual(root).Transform(new Point(0, 0));
        return new Rect(topLeft.X, topLeft.Y, element.ActualWidth, element.ActualHeight);
    }

    private static bool HasClippingAncestor(FrameworkElement element)
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement fe && fe.ClipToBounds)
                return true;
        }

        return false;
    }
}
