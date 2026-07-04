using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public readonly record struct RasterEdgeClearanceResult(
    double LeftClearancePx,
    double RightClearancePx,
    double TopClearancePx,
    double BottomClearancePx,
    int MissingEdgeCount);

public static class RasterEdgeClearanceAnalyzer
{
    public static RasterEdgeClearanceResult Analyze(BitmapSource bitmap, RasterPlacement placement) =>
        AnalyzePageBitmap(bitmap, placement, WeighTicketPrintLayout.PageWidthDip, WeighTicketPrintLayout.PageHeightDip, BuildA4TwoUpSafeRegions);

    public static RasterEdgeClearanceResult AnalyzeA5Ticket(BitmapSource bitmap, RasterPlacement placement) =>
        AnalyzePageBitmap(
            bitmap,
            placement,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip,
            BuildA5TicketSafeRegions);

    public static RasterEdgeClearanceResult AnalyzeA5Page(BitmapSource bitmap, RasterPlacement placement) =>
        AnalyzeA5Page(bitmap, placement, WeighTicketPrintLayout.A5LandscapePageWidthDip, WeighTicketPrintLayout.A5LandscapePageHeightDip);

    public static RasterEdgeClearanceResult AnalyzeA5Page(
        BitmapSource bitmap,
        RasterPlacement placement,
        double pageWidthDip,
        double pageHeightDip) =>
        AnalyzePageBitmap(
            bitmap,
            placement,
            pageWidthDip,
            pageHeightDip,
            (scaleX, scaleY) => BuildTargetRegion(scaleX, scaleY, placement));

    private static RasterEdgeClearanceResult AnalyzePageBitmap(
        BitmapSource bitmap,
        RasterPlacement placement,
        double sourceWidthDip,
        double sourceHeightDip,
        Func<double, double, IReadOnlyList<(int LeftPx, int TopPx, int RightPx, int BottomPx)>> buildRegions)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        if (width <= 0 || height <= 0)
            return new RasterEdgeClearanceResult(0, 0, 0, 0, 4);

        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var scaleX = width / sourceWidthDip;
        var scaleY = height / sourceHeightDip;
        var regions = buildRegions(scaleX, scaleY);

        var minX = width;
        var maxX = -1;
        var minY = height;
        var maxY = -1;

        foreach (var region in regions)
        {
            var xStart = Math.Clamp(region.LeftPx, 0, width - 1);
            var xEnd = Math.Clamp(region.RightPx, 0, width);
            var yStart = Math.Clamp(region.TopPx, 0, height - 1);
            var yEnd = Math.Clamp(region.BottomPx, 0, height);

            for (var y = yStart; y < yEnd; y++)
            {
                for (var x = xStart; x < xEnd; x++)
                {
                    var i = y * stride + x * 4;
                    var a = pixels[i + 3];
                    var r = pixels[i + 2];
                    var g = pixels[i + 1];
                    var b = pixels[i];
                    if (a > 16 && (r < 245 || g < 245 || b < 245))
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }

        if (maxX < 0)
            return new RasterEdgeClearanceResult(0, 0, 0, 0, 4);

        var contentLeftDip = minX / scaleX;
        var contentRightDip = (maxX + 1) / scaleX;
        var contentTopDip = minY / scaleY;
        var contentBottomDip = (maxY + 1) / scaleY;

        var printLeftDip = placement.LeftDip + contentLeftDip * placement.ScaleFactor;
        var printRightDip = placement.LeftDip + contentRightDip * placement.ScaleFactor;
        var printTopDip = placement.TopDip + contentTopDip * placement.ScaleFactor;
        var printBottomDip = placement.TopDip + contentBottomDip * placement.ScaleFactor;

        var leftClearance = (printLeftDip - placement.TargetLeftDip) * scaleX;
        var rightClearance = (placement.TargetLeftDip + placement.TargetWidthDip - printRightDip) * scaleX;
        var topClearance = (printTopDip - placement.TargetTopDip) * scaleY;
        var bottomClearance = (placement.TargetTopDip + placement.TargetHeightDip - printBottomDip) * scaleY;

        var missing = 0;
        if (leftClearance < 0) missing++;
        if (rightClearance < 0) missing++;
        if (topClearance < 0) missing++;
        if (bottomClearance < 0) missing++;

        return new RasterEdgeClearanceResult(leftClearance, rightClearance, topClearance, bottomClearance, missing);
    }

    private static IReadOnlyList<(int LeftPx, int TopPx, int RightPx, int BottomPx)> BuildA4TwoUpSafeRegions(double scaleX, double scaleY)
    {
        var regions = new List<(int, int, int, int)>(2);
        foreach (var topDip in new[] { WeighTicketPrintLayout.TopCopyTopDip, WeighTicketPrintLayout.BottomCopyTopDip })
        {
            var leftPx = (int)Math.Floor(WeighTicketPrintLayout.CopySafeLeftEdgeDip * scaleX);
            var rightPx = (int)Math.Ceiling(WeighTicketPrintLayout.CopySafeRightEdgeDip * scaleX);
            var topPx = (int)Math.Floor((topDip + WeighTicketPrintLayout.CopySafeTopEdgeDip) * scaleY);
            var bottomPx = (int)Math.Ceiling((topDip + WeighTicketPrintLayout.CopySafeBottomEdgeDip) * scaleY);
            regions.Add((leftPx, topPx, rightPx, bottomPx));
        }

        return regions;
    }

    private static IReadOnlyList<(int LeftPx, int TopPx, int RightPx, int BottomPx)> BuildA5TicketSafeRegions(double scaleX, double scaleY)
    {
        var leftPx = (int)Math.Floor(WeighTicketPrintLayout.CopySafeLeftEdgeDip * scaleX);
        var rightPx = (int)Math.Ceiling(WeighTicketPrintLayout.CopySafeRightEdgeDip * scaleX);
        var topPx = (int)Math.Floor(WeighTicketPrintLayout.CopySafeTopEdgeDip * scaleY);
        var bottomPx = (int)Math.Ceiling(WeighTicketPrintLayout.CopySafeBottomEdgeDip * scaleY);
        return [(leftPx, topPx, rightPx, bottomPx)];
    }

    private static IReadOnlyList<(int LeftPx, int TopPx, int RightPx, int BottomPx)> BuildTargetRegion(
        double scaleX,
        double scaleY,
        RasterPlacement placement)
    {
        var leftPx = (int)Math.Floor(placement.TargetLeftDip * scaleX);
        var topPx = (int)Math.Floor(placement.TargetTopDip * scaleY);
        var rightPx = (int)Math.Ceiling((placement.TargetLeftDip + placement.TargetWidthDip) * scaleX);
        var bottomPx = (int)Math.Ceiling((placement.TargetTopDip + placement.TargetHeightDip) * scaleY);
        return [(leftPx, topPx, rightPx, bottomPx)];
    }
}
