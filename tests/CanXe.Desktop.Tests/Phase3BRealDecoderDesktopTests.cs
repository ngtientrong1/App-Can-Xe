using System.IO;
using CanXe.Application.Configuration;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase3BRealDecoderDesktopTests(WpfSmokeFixture wpf)
{
    [Fact]
    public void BitmapSource_FromJpeg_IsFrozen()
    {
        _ = wpf;
        var jpeg = CreateTestJpeg(320, 240, 7);
        var bitmap = CameraBitmapFactory.FromJpeg(jpeg);
        Assert.NotNull(bitmap);
        Assert.True(bitmap!.IsFrozen);
    }

    [Fact]
    public async Task FrameDecoded_UpdatesCameraPreviewFrameOnUiThread()
    {
        _ = wpf;
        var camera = new ControllableCameraStreamService();
        await using var host = new CameraDesktopTestHost(camera);
        var (vm, settings) = await host.CreateMainViewModelAsync();

        settings.CameraEnabled = true;
        settings.RtspHost = "192.168.1.50";
        settings.AutoConnectCameraOnStartup = true;
        await settings.SaveCameraCommand.ExecuteAsync(null);

        await vm.InitializeAsync();
        await WaitForConditionAsync(() => vm.CameraPreviewFrame is not null, TimeSpan.FromSeconds(15));

        Assert.NotNull(vm.CameraPreviewFrame);
        Assert.True(vm.CameraPreviewFrame!.IsFrozen);
        Assert.True(vm.CameraPreviewAvailable);
    }

    private static byte[] CreateTestJpeg(int width, int height, int seed)
    {
        using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.FromArgb((seed * 37) % 255, (seed * 53) % 255, (seed * 71) % 255));
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        return ms.ToArray();
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            if (condition())
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }
}
