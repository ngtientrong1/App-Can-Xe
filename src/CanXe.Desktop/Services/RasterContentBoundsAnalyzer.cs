using System.Windows.Media.Imaging;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public readonly record struct RasterContentBoundsResult(
    ContentBoundsDip MainContentBoundsDip,
    ContentBoundsDip? WatermarkBoundsDip);

public static class RasterContentBoundsAnalyzer
{
    public static RasterContentBoundsResult AnalyzeTicketBitmap(BitmapSource bitmap, bool scanWatermark) =>
        AnalyzeTicketBitmap(
            bitmap,
            scanWatermark,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);

    public static RasterContentBoundsResult AnalyzeTicketBitmap(
        BitmapSource bitmap,
        bool scanWatermark,
        double sourceWidthDip,
        double sourceHeightDip)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        if (width <= 0 || height <= 0)
        {
            return new RasterContentBoundsResult(
                ContentBoundsDip.FromSize(sourceWidthDip, sourceHeightDip),
                null);
        }

        var scaleX = width / sourceWidthDip;
        var scaleY = height / sourceHeightDip;

        var mainRegion = BuildMainContentRegion(scaleX, scaleY);
        var mainBoundsPx = ScanNonWhiteBounds(bitmap, [mainRegion]);
        var mainBoundsDip = PxBoundsToDip(mainBoundsPx, scaleX, scaleY, sourceWidthDip, sourceHeightDip);

        ContentBoundsDip? watermarkBoundsDip = null;
        if (scanWatermark)
        {
            var watermarkBoundsPx = ScanLowOpacityBounds(bitmap, [BuildWatermarkRegion(scaleX, scaleY)]);
            if (watermarkBoundsPx.HasContent && watermarkBoundsPx.PixelCount >= 10)
                watermarkBoundsDip = PxBoundsToDip(watermarkBoundsPx, scaleX, scaleY, sourceWidthDip, sourceHeightDip);
        }

        return new RasterContentBoundsResult(mainBoundsDip, watermarkBoundsDip);
    }

    private static (int Left, int Top, int Right, int Bottom) BuildMainContentRegion(double scaleX, double scaleY)
    {
        var leftPx = (int)Math.Floor(WeighTicketPrintLayout.CopySafeLeftEdgeDip * scaleX);
        var rightPx = (int)Math.Ceiling(WeighTicketPrintLayout.CopySafeRightEdgeDip * scaleX);
        var topPx = (int)Math.Floor(WeighTicketPrintLayout.CopySafeTopEdgeDip * scaleY);
        var bottomPx = (int)Math.Ceiling(WeighTicketPrintLayout.CopySafeBottomEdgeDip * scaleY);
        return (leftPx, topPx, rightPx, bottomPx);
    }

    private static (int Left, int Top, int Right, int Bottom) BuildWatermarkRegion(double scaleX, double scaleY)
    {
        var leftPx = 0;
        var rightPx = (int)Math.Ceiling(WeighTicketPrintLayout.TicketLogicalWidthDip * scaleX);
        var topPx = 0;
        var bottomPx = (int)Math.Ceiling(WeighTicketPrintLayout.TicketLogicalHeightDip * scaleY);
        return (leftPx, topPx, rightPx, bottomPx);
    }

    private static ContentBoundsDip PxBoundsToDip(
        PxBounds bounds,
        double scaleX,
        double scaleY,
        double sourceWidthDip,
        double sourceHeightDip)
    {
        if (!bounds.HasContent)
            return ContentBoundsDip.FromSize(sourceWidthDip, sourceHeightDip);

        return new ContentBoundsDip(
            bounds.Left / scaleX,
            bounds.Top / scaleY,
            (bounds.Right + 1) / scaleX,
            (bounds.Bottom + 1) / scaleY);
    }

    private static PxBounds ScanNonWhiteBounds(
        BitmapSource bitmap,
        IReadOnlyList<(int Left, int Top, int Right, int Bottom)> regions,
        byte maxOpacity = 16)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var minX = width;
        var maxX = -1;
        var minY = height;
        var maxY = -1;

        foreach (var region in regions)
        {
            var xStart = Math.Clamp(region.Left, 0, width - 1);
            var xEnd = Math.Clamp(region.Right, 0, width);
            var yStart = Math.Clamp(region.Top, 0, height - 1);
            var yEnd = Math.Clamp(region.Bottom, 0, height);

            for (var y = yStart; y < yEnd; y++)
            {
                for (var x = xStart; x < xEnd; x++)
                {
                    var i = y * stride + x * 4;
                    var b = pixels[i];
                    var g = pixels[i + 1];
                    var r = pixels[i + 2];
                    var a = pixels[i + 3];
                    if (a > maxOpacity && (r < 245 || g < 245 || b < 245))
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
            return new PxBounds(0, 0, -1, -1, 0);

        return new PxBounds(minX, minY, maxX, maxY, (maxX - minX + 1) * (maxY - minY + 1));
    }

    private static PxBounds ScanLowOpacityBounds(
        BitmapSource bitmap,
        IReadOnlyList<(int Left, int Top, int Right, int Bottom)> regions)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var minX = width;
        var maxX = -1;
        var minY = height;
        var maxY = -1;
        var count = 0;

        foreach (var region in regions)
        {
            var xStart = Math.Clamp(region.Left, 0, width - 1);
            var xEnd = Math.Clamp(region.Right, 0, width);
            var yStart = Math.Clamp(region.Top, 0, height - 1);
            var yEnd = Math.Clamp(region.Bottom, 0, height);

            for (var y = yStart; y < yEnd; y++)
            {
                for (var x = xStart; x < xEnd; x++)
                {
                    var i = y * stride + x * 4;
                    var b = pixels[i];
                    var g = pixels[i + 1];
                    var r = pixels[i + 2];
                    var a = pixels[i + 3];
                    if (a is >= 2 and <= 80 && (r < 250 || g < 250 || b < 250))
                    {
                        count++;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }

        if (maxX < 0)
            return new PxBounds(0, 0, -1, -1, 0);

        return new PxBounds(minX, minY, maxX, maxY, count);
    }

    private readonly record struct PxBounds(int Left, int Top, int Right, int Bottom, int PixelCount)
    {
        public bool HasContent => Right >= 0 && Bottom >= 0 && PixelCount > 0;
    }
}
