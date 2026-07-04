using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CanXe.Desktop.Services;

public static class A5RasterBitmapRotator
{
    public static BitmapSource RotateClockwise90(BitmapSource source)
    {
        var rotatedWidth = source.PixelHeight;
        var rotatedHeight = source.PixelWidth;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new TranslateTransform(rotatedWidth, 0));
            context.PushTransform(new RotateTransform(90));
            context.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
        }

        var bitmap = new RenderTargetBitmap(
            rotatedWidth,
            rotatedHeight,
            source.DpiX,
            source.DpiY,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
