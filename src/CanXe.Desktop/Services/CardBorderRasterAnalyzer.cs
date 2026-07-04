using System.Windows;
using System.Windows.Media.Imaging;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed record CardRasterEdgeResult(
    string CardName,
    int EdgeCount,
    bool TopPresent,
    bool RightPresent,
    bool BottomPresent,
    bool LeftPresent);

public sealed record CardRasterAnalysisResult(
    IReadOnlyList<CardRasterEdgeResult> Cards,
    int MissingEdgeCount,
    int DoubleBorderCount,
    int ConnectedCardBorderCount)
{
    public static CardRasterAnalysisResult Empty { get; } = new([], 0, 0, 0);
}

public static class CardBorderRasterAnalyzer
{
    private const byte DarkThreshold = 160;
    private const double EdgeCoverageMin = 0.40;

    private const byte NonWhiteChannelMax = 245;

    public static CardRasterAnalysisResult AnalyzeCopyRegion(
        BitmapSource bitmap,
        Rect copyBoundsOnPageDip,
        IReadOnlyDictionary<string, Rect> cardBoundsOnPageDip)
    {
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0 || cardBoundsOnPageDip.Count == 0)
            return CardRasterAnalysisResult.Empty;

        var scaleX = bitmap.PixelWidth / WeighTicketPrintLayout.TicketLogicalWidthDip;
        var scaleY = bitmap.PixelHeight / WeighTicketPrintLayout.TicketLogicalHeightDip;
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var cards = new List<CardRasterEdgeResult>();
        var missing = 0;
        foreach (var (name, bounds) in cardBoundsOnPageDip)
        {
            var edge = AnalyzeCardEdges(pixels, stride, bitmap.PixelWidth, bitmap.PixelHeight, bounds, scaleX, scaleY, name);
            cards.Add(edge);
            missing += 4 - edge.EdgeCount;
        }

        var doubleCount = CountDoubleBorders(pixels, stride, bitmap.PixelWidth, bitmap.PixelHeight, cardBoundsOnPageDip, scaleX, scaleY);
        var connected = CountConnectedBorders(cardBoundsOnPageDip);

        return new CardRasterAnalysisResult(cards, missing, doubleCount, connected);
    }

    private static CardRasterEdgeResult AnalyzeCardEdges(
        byte[] pixels,
        int stride,
        int width,
        int height,
        Rect boundsDip,
        double scaleX,
        double scaleY,
        string cardName)
    {
        var left = (int)Math.Round(boundsDip.Left * scaleX);
        var top = (int)Math.Round(boundsDip.Top * scaleY);
        var right = (int)Math.Round(boundsDip.Right * scaleX) - 1;
        var bottom = (int)Math.Round(boundsDip.Bottom * scaleY) - 1;

        left = Math.Clamp(left, 0, width - 1);
        right = Math.Clamp(right, left, width - 1);
        top = Math.Clamp(top, 0, height - 1);
        bottom = Math.Clamp(bottom, top, height - 1);

        var topPresent = ScanHorizontalBand(pixels, stride, width, left, right, top, top + 3);
        var bottomPresent = ScanHorizontalBand(pixels, stride, width, left, right, bottom - 3, bottom);
        var leftPresent = ScanVerticalBand(pixels, stride, height, top, bottom, left, left + 3);
        var rightPresent = ScanVerticalBand(pixels, stride, height, top, bottom, right - 3, right);

        var edgeCount = (topPresent ? 1 : 0) + (rightPresent ? 1 : 0) + (bottomPresent ? 1 : 0) + (leftPresent ? 1 : 0);
        return new CardRasterEdgeResult(
            cardName,
            edgeCount,
            topPresent,
            rightPresent,
            bottomPresent,
            leftPresent);
    }

    private static bool ScanHorizontalBand(byte[] pixels, int stride, int width, int xStart, int xEnd, int yStart, int yEnd)
    {
        if (xEnd <= xStart)
            return false;

        for (var y = yStart; y <= yEnd; y++)
        {
            if (ScanHorizontal(pixels, stride, width, xStart, xEnd, y))
                return true;
        }

        return false;
    }

    private static bool ScanVerticalBand(byte[] pixels, int stride, int height, int yStart, int yEnd, int xStart, int xEnd)
    {
        if (yEnd <= yStart)
            return false;

        for (var x = xStart; x <= xEnd; x++)
        {
            if (ScanVertical(pixels, stride, height, yStart, yEnd, x))
                return true;
        }

        return false;
    }

    private static bool ScanHorizontal(byte[] pixels, int stride, int width, int xStart, int xEnd, int y)
    {
        if (xEnd <= xStart)
            return false;

        var dark = 0;
        for (var x = xStart; x <= xEnd; x++)
        {
            if (IsDark(pixels, stride, x, y))
                dark++;
        }

        var total = xEnd - xStart + 1;
        return dark / (double)total >= EdgeCoverageMin;
    }

    private static bool ScanVertical(byte[] pixels, int stride, int height, int yStart, int yEnd, int x)
    {
        if (yEnd <= yStart)
            return false;

        var dark = 0;
        for (var y = yStart; y <= yEnd; y++)
        {
            if (IsDark(pixels, stride, x, y))
                dark++;
        }

        var total = yEnd - yStart + 1;
        return dark / (double)total >= EdgeCoverageMin;
    }

    private static bool IsDark(byte[] pixels, int stride, int x, int y)
    {
        var i = y * stride + x * 4;
        if (i < 0 || i + 3 >= pixels.Length)
            return false;

        var b = pixels[i];
        var g = pixels[i + 1];
        var r = pixels[i + 2];
        var a = pixels[i + 3];
        return a > 16 && (r < NonWhiteChannelMax || g < NonWhiteChannelMax || b < NonWhiteChannelMax);
    }

    private static int CountDoubleBorders(
        byte[] pixels,
        int stride,
        int width,
        int height,
        IReadOnlyDictionary<string, Rect> cards,
        double scaleX,
        double scaleY)
    {
        var count = 0;
        foreach (var bounds in cards.Values)
        {
            var left = (int)Math.Round(bounds.Left * scaleX);
            var top = (int)Math.Round(bounds.Top * scaleY);
            var right = (int)Math.Round(bounds.Right * scaleX) - 1;
            var bottom = (int)Math.Round(bounds.Bottom * scaleY) - 1;
            if (HasDoubleHorizontal(pixels, stride, width, left, right, top) ||
                HasDoubleHorizontal(pixels, stride, width, left, right, bottom) ||
                HasDoubleVertical(pixels, stride, height, top, bottom, left) ||
                HasDoubleVertical(pixels, stride, height, top, bottom, right))
                count++;
        }

        return count;
    }

    private static bool HasDoubleHorizontal(byte[] pixels, int stride, int width, int left, int right, int y)
    {
        if (right <= left)
            return false;

        var bands = 0;
        for (var offset = 0; offset <= 2; offset++)
        {
            var yy = y + offset;
            if (yy < 0 || yy >= pixels.Length / stride)
                continue;
            if (ScanHorizontal(pixels, stride, width, left + 1, right - 1, yy))
                bands++;
        }

        return bands >= 2 && bands <= 3;
    }

    private static bool HasDoubleVertical(byte[] pixels, int stride, int height, int top, int bottom, int x)
    {
        if (bottom <= top)
            return false;

        var bands = 0;
        for (var offset = 0; offset <= 2; offset++)
        {
            var xx = x + offset;
            if (ScanVertical(pixels, stride, height, top + 1, bottom - 1, xx))
                bands++;
        }

        return bands >= 2 && bands <= 3;
    }

    private static int CountConnectedBorders(IReadOnlyDictionary<string, Rect> cards)
    {
        var list = cards.Values.ToList();
        var count = 0;
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                if (RectsTouch(list[i], list[j]))
                    count++;
            }
        }

        return count;
    }

    private static bool RectsTouch(Rect a, Rect b)
    {
        const double eps = 0.01;
        var horizontalOverlap = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left) > eps;
        var verticalOverlap = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top) > eps;
        var touchVertical = horizontalOverlap &&
                            (Math.Abs(a.Bottom - b.Top) <= eps || Math.Abs(b.Bottom - a.Top) <= eps);
        var touchHorizontal = verticalOverlap &&
                              (Math.Abs(a.Right - b.Left) <= eps || Math.Abs(b.Right - a.Left) <= eps);
        return touchVertical || touchHorizontal;
    }
}
