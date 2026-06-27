using System.IO;
using CanXe.DeviceTester.Core;
using CanXe.DeviceTester.Core.Formatting;
using CanXe.DeviceTester.Core.Models;
using CanXe.DeviceTester.Core.Services;
using CanXe.DeviceTester.Tests.Support;

namespace CanXe.DeviceTester.Tests;

public class Phase2ADeviceTesterTests
{
    [Fact]
    public void Discovery_ListsPorts()
    {
        var discovery = new FakeSerialPortDiscoveryService();
        var ports = discovery.GetAvailablePortNames();
        Assert.Contains("COM1", ports);
        Assert.Contains("COM2", ports);
    }

    [Fact]
    public void DefaultSettings_AreCom1_9600_8N1()
    {
        var settings = SerialPortSettings.CreateDefault();
        Assert.Equal("COM1", settings.PortName);
        Assert.Equal(9600, settings.BaudRate);
        Assert.Equal(8, settings.DataBits);
        Assert.Equal("None", settings.Parity);
        Assert.Equal("One", settings.StopBits);
        Assert.Equal("None", settings.Handshake);
    }

    [Fact]
    public void Startup_DoesNotAutoOpenPort()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        Assert.False(provider.IsOpen);
        Assert.Equal(SerialConnectionStatus.Closed, service.Status);
    }

    [Fact]
    public async Task Open_UsesConfiguredSettings()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        service.UpdateSettings(new SerialPortSettings { PortName = "COM3", BaudRate = 19200 });
        await service.OpenPortAsync();
        Assert.Equal("COM3", provider.LastOpenSettings!.PortName);
        Assert.Equal(19200, provider.LastOpenSettings.BaudRate);
    }

    [Fact]
    public async Task Close_DisposesPort()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        await service.OpenPortAsync();
        await service.ClosePortAsync();
        Assert.Equal(1, provider.CloseCount);
        Assert.False(provider.IsOpen);
    }

    [Fact]
    public void Provider_HasNoWriteMethod()
    {
        var write = typeof(ISerialPortProvider).GetMethod("Write");
        Assert.Null(write);
    }

    [Fact]
    public void RawBytes_ArePreservedInChunk()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        var input = new byte[] { 0x02, 0x53, 0x54, 0x0D, 0x0A };
        provider.SimulateDataReceived(input);
        service.ApplyPendingUiUpdates();
        var chunk = service.DisplayChunks.Single();
        Assert.Equal(input, chunk.RawBytes);
    }

    [Fact]
    public void HexConversion_IsAccurate()
    {
        var hex = SerialHexFormatter.ToHexString(new byte[] { 0x02, 0x53, 0x2C });
        Assert.Equal("02 53 2C", hex);
    }

    [Fact]
    public void EscapedText_HandlesControlBytes()
    {
        var escaped = SerialTextEscaper.Escape(new byte[] { 0x53, 0x54, 0x0D, 0x0A, 0x00, 0x02 });
        Assert.Equal("ST\\r\\n\\0\\x02", escaped);
    }

    [Fact]
    public void DataReceivedEvents_AreSeparateChunks()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        provider.SimulateDataReceived([0x01]);
        provider.SimulateDataReceived([0x02, 0x03]);
        service.ApplyPendingUiUpdates();
        Assert.Equal(2, service.DisplayChunks.Count);
        Assert.Equal(1, service.DisplayChunks[0].ByteCount);
        Assert.Equal(2, service.DisplayChunks[1].ByteCount);
    }

    [Fact]
    public async Task Log_ContainsRequiredFields()
    {
        using var provider = new FakeSerialPortProvider();
        using var logWriter = new RawSerialLogWriter();
        using var service = new SerialCaptureService(provider, new FakeSerialPortDiscoveryService(), logWriter);
        service.UpdateSessionMetadata(new SerialCaptureSession
        {
            PortSettings = SerialPortSettings.CreateDefault(),
            SessionLabel = SessionLabelPresets.StableWeight
        });
        service.StartRecording();
        provider.SimulateDataReceived([0x41, 0x0D, 0x0A]);
        var path = await service.SaveLogAsync(Path.GetTempPath());
        var text = await File.ReadAllTextAsync(path);
        Assert.Contains("Session started:", text);
        Assert.Contains("Timestamp:", text);
        Assert.Contains("Chunk number:", text);
        Assert.Contains("Byte count:", text);
        Assert.Contains("HEX:", text);
        Assert.Contains("Escaped text:", text);
        Assert.Contains("Session label: Trọng lượng ổn định", text);
        File.Delete(path);
    }

    [Fact]
    public void Log_ContainsSessionLabel()
    {
        using var logWriter = new RawSerialLogWriter();
        logWriter.StartSession(new SerialCaptureSession
        {
            PortSettings = SerialPortSettings.CreateDefault(),
            SessionLabel = SessionLabelPresets.LoadedVehicle
        });
        Assert.True(logWriter.IsRecording);
    }

    [Fact]
    public void DisplayChunks_AreLimitedTo5000()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        for (var i = 0; i < SerialCaptureUiLimits.MaxDisplayChunks + 10; i++)
            provider.SimulateDataReceived([0x01]);
        service.ApplyPendingUiUpdates();
        Assert.Equal(SerialCaptureUiLimits.MaxDisplayChunks, service.DisplayChunks.Count);
    }

    [Fact]
    public async Task PauseDisplay_StillWritesLog()
    {
        using var provider = new FakeSerialPortProvider();
        using var logWriter = new RawSerialLogWriter();
        using var service = new SerialCaptureService(provider, new FakeSerialPortDiscoveryService(), logWriter);
        service.StartRecording();
        service.IsDisplayPaused = true;
        provider.SimulateDataReceived([0x10]);
        service.ApplyPendingUiUpdates();
        Assert.Empty(service.DisplayChunks);
        var path = await service.SaveLogAsync(Path.GetTempPath());
        var text = await File.ReadAllTextAsync(path);
        Assert.Contains("HEX: 10", text);
        File.Delete(path);
    }

    [Fact]
    public void AccessDenied_ShowsFriendlyMessage()
    {
        var mapped = SerialPortExceptionMapper.Map(new UnauthorizedAccessException(), "COM1");
        Assert.Equal(SerialConnectionStatus.PortBusy, mapped.Status);
        Assert.Contains("COM1", mapped.Message);
    }

    [Fact]
    public void PortBusy_ShowsCloseOldSoftwareHint()
    {
        var mapped = SerialPortExceptionMapper.Map(
            new InvalidOperationException("Access to the port 'COM1' is denied."),
            "COM1");
        Assert.Contains("phần mềm cân cũ", mapped.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadFailure_DoesNotCrashService()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        provider.SimulateReadFailed(new IOException("disconnected"));
        Assert.Equal(SerialConnectionStatus.Disconnected, service.Status);
    }

    [Fact]
    public async Task Dispose_ClosesSerialPort()
    {
        var provider = new FakeSerialPortProvider();
        var service = CreateService(provider);
        await service.OpenPortAsync();
        service.Dispose();
        Assert.False(provider.IsOpen);
    }

    [Fact]
    public void FakeProvider_TracksNoWritesThroughInterface()
    {
        using var provider = new FakeSerialPortProvider();
        provider.SimulateWriteAttempt();
        Assert.Equal(1, provider.BytesWritten);
        Assert.Null(typeof(ISerialPortProvider).GetMethod("Write"));
    }

    [Fact]
    public void PublishProfile_DeviceTesterWin10_Exists()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var path = Path.Combine(repoRoot, "src", "CanXe.DeviceTester", "Properties", "PublishProfiles", "DeviceTesterWin10.pubxml");
        Assert.True(File.Exists(path), $"Missing publish profile: {path}");
    }

    private static SerialCaptureService CreateService(FakeSerialPortProvider provider) =>
        new(provider, new FakeSerialPortDiscoveryService(), new RawSerialLogWriter());
}
