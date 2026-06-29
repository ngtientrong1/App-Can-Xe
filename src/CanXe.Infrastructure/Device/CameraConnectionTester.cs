using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;

namespace CanXe.Infrastructure.Device;

public sealed class CameraConnectionTester : ICameraConnectionTester
{
    private readonly ICameraStreamService _streamService;

    public CameraConnectionTester(ICameraStreamService streamService) =>
        _streamService = streamService;

    public Task<ConnectionTestResult> TestAsync(
        CameraDeviceSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = CameraSettingsMapper.NormalizeLoaded(settings);
        var runtime = CameraSettingsMapper.ToRuntime(normalized, settings.Password);
        return _streamService.TestAsync(runtime, cancellationToken);
    }
}
