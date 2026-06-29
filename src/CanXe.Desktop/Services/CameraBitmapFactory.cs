using System.IO;
using System.Windows.Media.Imaging;

namespace CanXe.Desktop.Services;

public static class CameraBitmapFactory
{
    public static BitmapSource? FromJpeg(byte[] jpegBytes)
    {
        if (jpegBytes.Length == 0)
            return null;

        using var stream = new MemoryStream(jpegBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
