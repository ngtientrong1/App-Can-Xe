using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Infrastructure.Camera;

namespace CanXe.Tests.Support;

internal static class TestCameraStreamFactory
{
    public static CameraStreamService Create(AppSettings settings, ICameraDecoderFactory factory) =>
        new(settings, factory, new LatestCameraFrameCache());
}
