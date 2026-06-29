using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Domain.Services;

namespace CanXe.Infrastructure.Camera;

public static class CameraDecoderGuard
{
    public static void EnsureValid(string? deviceMode, ICameraDecoder decoder)
    {
        if (!ProductionConfigPolicy.IsHardwareMode(deviceMode))
            return;

        if (decoder is FakeCameraDecoder)
        {
            throw new InvalidOperationException(
                "Hardware device mode requires FfmpegRtspDecoder. " +
                "FakeCameraDecoder must not be registered when DeviceMode is Hardware.");
        }
    }
}

public sealed class CameraDecoderFactory(
    AppSettings appSettings,
    Func<ICameraDecoder>? testDecoderOverride = null) : ICameraDecoderFactory
{
    public ICameraDecoder CreateDecoder()
    {
        ICameraDecoder decoder;
        if (ProductionConfigPolicy.IsHardwareMode(appSettings.DeviceMode))
        {
            decoder = new FfmpegRtspDecoder();
        }
        else if (ProductionConfigPolicy.IsTestMode(appSettings.DeviceMode))
        {
            decoder = testDecoderOverride?.Invoke()
                ?? throw new InvalidOperationException(
                    "Test device mode requires an explicit decoder override.");
        }
        else
        {
            decoder = new FakeCameraDecoder();
        }

        CameraDecoderGuard.EnsureValid(appSettings.DeviceMode, decoder);
        return decoder;
    }
}
