using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CanXe.Desktop.Services;

public static class ClipboardImageService
{
    public static void CopyPngToClipboard(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        Clipboard.SetImage(image);
    }
}
