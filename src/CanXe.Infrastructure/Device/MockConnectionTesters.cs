using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Infrastructure.Device;

public sealed class MockCameraConnectionTester : ICameraConnectionTester
{
    private readonly bool _simulateFailure;

    public MockCameraConnectionTester(bool simulateFailure = false) => _simulateFailure = simulateFailure;

    public Task<ConnectionTestResult> TestAsync(
        CameraDeviceSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsEnabled)
            return Task.FromResult(ConnectionTestResult.Failed("Camera đang tắt trong cấu hình."));

        if (string.IsNullOrWhiteSpace(settings.RtspUrl))
            return Task.FromResult(ConnectionTestResult.Failed("Chưa nhập RTSP URL."));

        if (_simulateFailure)
            return Task.FromResult(ConnectionTestResult.Failed("Mock: không kết nối được RTSP."));

        return Task.FromResult(ConnectionTestResult.Succeeded("Mock: RTSP phản hồi thành công."));
    }
}

public sealed class MockScaleConnectionTester : IScaleConnectionTester
{
    private readonly bool _simulateFailure;

    public MockScaleConnectionTester(bool simulateFailure = false) => _simulateFailure = simulateFailure;

    public Task<ConnectionTestResult> TestAsync(
        ScaleDeviceSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        if (_simulateFailure)
            return Task.FromResult(ConnectionTestResult.Failed("Mock: không mở được cổng COM."));

        return Task.FromResult(ConnectionTestResult.Succeeded(
            $"Mock: cổng {settings.PortName} @ {settings.BaudRate} sẵn sàng (Simulation)."));
    }
}
