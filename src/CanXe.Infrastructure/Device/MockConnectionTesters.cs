using System.IO.Ports;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Scale;

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

public sealed class ScaleConnectionTester : IScaleConnectionTester
{
    private readonly AppSettings _appSettings;

    public ScaleConnectionTester(AppSettings appSettings) => _appSettings = appSettings;

    public Task<ConnectionTestResult> TestAsync(
        ScaleDeviceSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        if (ScaleInputModeDisplay.IsHardwareDeviceMode(settings.DeviceMode)
            || ScaleInputModeDisplay.IsHardwareDeviceMode(_appSettings.DeviceMode))
            return TestHardwarePortAsync(settings, cancellationToken);

        return TestSimulationAsync(settings, _appSettings.SimulateScaleDisconnect);
    }

    private static Task<ConnectionTestResult> TestSimulationAsync(
        ScaleDeviceSettingsDto settings,
        bool simulateFailure)
    {
        if (simulateFailure)
            return Task.FromResult(ConnectionTestResult.Failed("Mock: không mở được cổng COM."));

        return Task.FromResult(ConnectionTestResult.Succeeded(
            $"Mock: cổng {settings.PortName} @ {settings.BaudRate} sẵn sàng (Simulation)."));
    }

    private static Task<ConnectionTestResult> TestHardwarePortAsync(
        ScaleDeviceSettingsDto settings,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SerialPort? port = null;
            try
            {
                port = new SerialPort
                {
                    PortName = settings.PortName,
                    BaudRate = settings.BaudRate,
                    DataBits = settings.DataBits,
                    Parity = Enum.TryParse<Parity>(settings.Parity, true, out var parity) ? parity : Parity.None,
                    StopBits = Enum.TryParse<StopBits>(settings.StopBits, true, out var stopBits) ? stopBits : StopBits.One,
                    Handshake = Enum.TryParse<Handshake>(settings.Handshake, true, out var handshake) ? handshake : Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    DtrEnable = false,
                    RtsEnable = false
                };
                port.Open();
                ScaleConnectionLogger.Write("PortOpened", port: settings.PortName);
                return ConnectionTestResult.Succeeded(
                    $"Cổng {settings.PortName} @ {settings.BaudRate} mở được — dùng KẾT NỐI để nhận dữ liệu cân.");
            }
            catch (Exception ex)
            {
                ScaleConnectionLogger.Write("PortOpenFailed", detail: ex.Message, port: settings.PortName);
                return ConnectionTestResult.Failed($"Không mở được {settings.PortName}: {ex.Message}");
            }
            finally
            {
                if (port?.IsOpen == true)
                    port.Close();
                port?.Dispose();
            }
        }, cancellationToken);
    }
}
