using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class IdentityIconRasterReport
{
    public bool Passed { get; init; }
    public int PersonIconDetachedPixelCount { get; init; }
    public int VehicleIconDetachedPixelCount { get; init; }
    public bool PersonIconTouchesTileEdge { get; init; }
    public bool VehicleIconTouchesTileEdge { get; init; }
    public IReadOnlyList<string> Lines { get; init; } = [];
}

public static class IdentityIconRasterValidator
{
    private const byte WhiteChannelMin = 245;
    private const byte AlphaMin = 16;

    public static IdentityIconRasterReport Validate(WeighTicketCopyView copy)
    {
        WpfWeighTicketDocumentFactory.MaterializeTicket(copy);
        var lines = new List<string>();
        var failures = 0;

        var personTile = copy.FindName("CustomerIconTile") as FrameworkElement;
        var vehicleTile = copy.FindName("PlateIconTile") as FrameworkElement;
        var personPath = personTile is null ? null : FindFirstPath(personTile);
        var vehiclePath = vehicleTile is null ? null : FindFirstPath(vehicleTile);
        if (personTile is null || vehicleTile is null || personPath is null || vehiclePath is null)
        {
            return new IdentityIconRasterReport
            {
                Passed = false,
                Lines = ["FAIL: Missing icon tile/path elements."]
            };
        }

        var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
        var personDetached = CountDetachedPixelsBelowBody(copy, bitmap, personTile, personPath);
        var vehicleDetached = CountDetachedPixelsBelowBody(copy, bitmap, vehicleTile, vehiclePath);
        var personTouches = IconTouchesTileEdge(copy, personTile, personPath);
        var vehicleTouches = IconTouchesTileEdge(copy, vehicleTile, vehiclePath);

        lines.Add($"PersonIconDetachedPixelCount={personDetached}");
        lines.Add($"VehicleIconDetachedPixelCount={vehicleDetached}");
        lines.Add($"PersonIconTouchesTileEdge={personTouches}");
        lines.Add($"VehicleIconTouchesTileEdge={vehicleTouches}");

        if (personDetached > 0)
        {
            failures++;
            lines.Add($"FAIL: Person detached pixels={personDetached}");
        }

        if (vehicleDetached > 0)
        {
            failures++;
            lines.Add($"FAIL: Vehicle detached pixels={vehicleDetached}");
        }

        if (personTouches)
        {
            failures++;
            lines.Add("FAIL: Person icon touches tile edge.");
        }

        if (vehicleTouches)
        {
            failures++;
            lines.Add("FAIL: Vehicle icon touches tile edge.");
        }

        lines.Add(failures == 0 ? "PASS" : "FAIL");
        return new IdentityIconRasterReport
        {
            Passed = failures == 0,
            PersonIconDetachedPixelCount = personDetached,
            VehicleIconDetachedPixelCount = vehicleDetached,
            PersonIconTouchesTileEdge = personTouches,
            VehicleIconTouchesTileEdge = vehicleTouches,
            Lines = lines
        };
    }

    private static int CountDetachedPixelsBelowBody(
        Visual root,
        BitmapSource bitmap,
        FrameworkElement tile,
        Path path)
    {
        var tileBounds = GetBounds(root, tile);
        var iconBounds = GetPathGeometryBounds(root, path);
        if (iconBounds.IsEmpty)
            return 0;

        var scaleX = bitmap.PixelWidth / WeighTicketPrintLayout.TicketLogicalWidthDip;
        var scaleY = bitmap.PixelHeight / WeighTicketPrintLayout.TicketLogicalHeightDip;

        var left = (int)Math.Floor(tileBounds.Left * scaleX);
        var right = (int)Math.Ceiling(tileBounds.Right * scaleX);
        var top = (int)Math.Floor(tileBounds.Top * scaleY);
        var bottom = (int)Math.Ceiling(tileBounds.Bottom * scaleY);
        var bodyBottomPx = (int)Math.Floor(iconBounds.Bottom * scaleY);

        if (left >= right || top >= bottom)
            return 0;

        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var width = right - left;
        var height = bottom - top;
        var mask = new bool[width * height];
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var i = y * stride + x * 4;
                mask[(y - top) * width + (x - left)] = IsWhitePixel(pixels, i);
            }
        }

        var connected = FloodFillConnected(mask, width, height,
            (int)Math.Round((iconBounds.Left + iconBounds.Right) * 0.5 * scaleX) - left,
            (int)Math.Round((iconBounds.Top + iconBounds.Bottom) * 0.5 * scaleY) - top);

        var iconLeft = Math.Max(0, (int)Math.Floor(iconBounds.Left * scaleX) - left);
        var iconRight = Math.Min(width, (int)Math.Ceiling(iconBounds.Right * scaleX) - left);
        var detached = 0;
        var scanTop = Math.Max(0, bodyBottomPx - top + 1);
        for (var y = scanTop; y < height; y++)
        {
            for (var x = iconLeft; x < iconRight; x++)
            {
                var idx = y * width + x;
                if (mask[idx] && !connected[idx])
                    detached++;
            }
        }

        return detached;
    }

    private static bool[] FloodFillConnected(bool[] mask, int width, int height, int seedX, int seedY)
    {
        var connected = new bool[mask.Length];
        if (seedX < 0 || seedY < 0 || seedX >= width || seedY >= height)
            return connected;

        var seedIndex = seedY * width + seedX;
        if (!mask[seedIndex])
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!mask[y * width + x])
                        continue;
                    seedX = x;
                    seedY = y;
                    seedIndex = y * width + x;
                    goto FoundSeed;
                }
            }

            return connected;
        }

        FoundSeed:
        var stack = new Stack<(int X, int Y)>();
        stack.Push((seedX, seedY));
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            var idx = y * width + x;
            if (x < 0 || y < 0 || x >= width || y >= height || !mask[idx] || connected[idx])
                continue;

            connected[idx] = true;
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }

        return connected;
    }

    private static bool IconTouchesTileEdge(Visual root, FrameworkElement tile, Path path)
    {
        var tileBounds = GetBounds(root, tile);
        var iconBounds = GetPathGeometryBounds(root, path);
        var inset = WeighTicketPrintLayout.MmToDip(1.5);
        return iconBounds.Left < tileBounds.Left + inset - 0.25
            || iconBounds.Right > tileBounds.Right - inset + 0.25
            || iconBounds.Top < tileBounds.Top + inset - 0.25
            || iconBounds.Bottom > tileBounds.Bottom - inset + 0.25;
    }

    private static Rect GetPathGeometryBounds(Visual root, Path path)
    {
        path.UpdateLayout();
        var geoBounds = path.RenderedGeometry.Bounds;
        if (geoBounds.IsEmpty && path.Data is not null)
            geoBounds = path.Data.Bounds;

        if (geoBounds.IsEmpty)
            return Rect.Empty;

        var transform = path.TransformToAncestor(root);
        var topLeft = transform.Transform(geoBounds.TopLeft);
        var bottomRight = transform.Transform(new Point(geoBounds.Right, geoBounds.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private static bool IsWhitePixel(byte[] pixels, int i) =>
        pixels[i + 3] > AlphaMin
        && pixels[i + 2] >= WhiteChannelMin
        && pixels[i + 1] >= WhiteChannelMin
        && pixels[i] >= WhiteChannelMin;

    private static Path? FindFirstPath(DependencyObject root)
    {
        if (root is Path path)
            return path;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindFirstPath(child);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    private static Rect GetBounds(Visual root, FrameworkElement element)
    {
        var topLeft = element.TransformToVisual(root).Transform(new Point(0, 0));
        return new Rect(topLeft.X, topLeft.Y, element.ActualWidth, element.ActualHeight);
    }
}
