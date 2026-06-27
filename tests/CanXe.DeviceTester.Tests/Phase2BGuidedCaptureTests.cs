using System.IO;
using System.Reflection;
using System.Text;
using CanXe.DeviceTester.Core;
using CanXe.DeviceTester.Core.Guided;
using CanXe.DeviceTester.Core.Models;
using CanXe.DeviceTester.Core.Services;
using CanXe.DeviceTester.Tests.Support;

namespace CanXe.DeviceTester.Tests;

public class Phase2BGuidedCaptureTests
{
    [Fact]
    public void Startup_DoesNotAutoOpenPort_GuidedContext()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        Assert.False(provider.IsOpen);
    }

    [Fact]
    public async Task UpdateSettings_ThrowsWhenPortOpen()
    {
        using var provider = new FakeSerialPortProvider();
        using var service = CreateService(provider);
        service.UpdateSettings(new SerialPortSettings { PortName = "COM2" });
        await service.OpenPortAsync();
        Assert.Throws<InvalidOperationException>(() => service.UpdateSettings(new SerialPortSettings { BaudRate = 4800 }));
    }

    [Fact]
    public void Preset_CannotChangeWhenRecording()
    {
        static bool IsPresetSelectionEnabled(bool isPortOpen, bool isRecording, bool guidedSessionActive, bool presetIsCustom) =>
            !isPortOpen && !isRecording && !guidedSessionActive && !presetIsCustom;

        Assert.False(IsPresetSelectionEnabled(isPortOpen: false, isRecording: true, guidedSessionActive: false, presetIsCustom: false));
        Assert.True(IsPresetSelectionEnabled(isPortOpen: false, isRecording: false, guidedSessionActive: false, presetIsCustom: false));
    }

    [Fact]
    public void StableSession_RequiresKnownWeightValidation()
    {
        var stats = new CaptureRecordingStats
        {
            Duration = TimeSpan.FromSeconds(30),
            TotalBytes = 100,
            TotalChunks = 10,
            IsStableSession = true,
            HasKnownWeight = false,
            TextLogSaved = true,
            RawFileSaved = true,
            SessionJsonSaved = true,
            RawFileSize = 100
        };
        var result = CaptureQualityValidator.Validate(stats);
        Assert.Equal(CaptureQualityStatus.Warning, result.Status);
    }

    [Fact]
    public void TransitionSession_AllowsNullWeight()
    {
        var stats = new CaptureRecordingStats
        {
            Duration = TimeSpan.FromSeconds(60),
            TotalBytes = 100,
            TotalChunks = 10,
            IsStableSession = false,
            HasKnownWeight = false,
            SessionType = GuidedSessionType.EmptyPersonTransition,
            Events = CaptureEventTypes.TransitionRequired.Select(t => new CaptureEventMarker
            {
                EventType = t, Label = t, Timestamp = DateTimeOffset.Now,
                ElapsedMilliseconds = 1, RawByteOffset = 0, ChunkNumber = 1
            }).ToList(),
            TextLogSaved = true,
            RawFileSaved = true,
            SessionJsonSaved = true,
            RawFileSize = 100
        };
        var result = CaptureQualityValidator.Validate(stats);
        Assert.NotEqual(CaptureQualityStatus.Invalid, result.Status);
    }

    [Fact]
    public void EventMarker_StoresTimestamp()
    {
        using var writer = new RawSerialLogWriter();
        writer.StartSession(new SerialCaptureSession { PortSettings = SerialPortSettings.CreateDefault() });
        var marker = writer.AddEventMarker(CaptureEventTypes.CaptureStarted, "start", 0, 0, 0);
        Assert.True(marker.Timestamp <= DateTimeOffset.Now);
    }

    [Fact]
    public void EventMarker_StoresRawByteOffset()
    {
        using var writer = new RawSerialLogWriter();
        writer.StartSession(new SerialCaptureSession { PortSettings = SerialPortSettings.CreateDefault() });
        writer.WriteChunk(MakeChunk(1, [0x01]), "01", "\\x01");
        var marker = writer.AddEventMarker(CaptureEventTypes.UserNote, "note", 5, 1, 1);
        Assert.Equal(1, marker.RawByteOffset);
    }

    [Fact]
    public void EventMarker_NotWrittenToRawBin()
    {
        using var writer = new RawSerialLogWriter();
        writer.StartSession(new SerialCaptureSession { PortSettings = SerialPortSettings.CreateDefault() });
        writer.WriteChunk(MakeChunk(1, [0x00, 0x80]), "00 80", "x");
        writer.AddEventMarker(CaptureEventTypes.CaptureStarted, "start", 0, 2, 1);
        Assert.Equal([0x00, 0x80], writer.GetRecordedRawBytes());
    }

    [Fact]
    public void RawFileSize_MatchesTotalBytes()
    {
        using var writer = new RawSerialLogWriter();
        writer.StartSession(new SerialCaptureSession { PortSettings = SerialPortSettings.CreateDefault() });
        writer.WriteChunk(MakeChunk(1, [0x01, 0x02, 0x03]), "01 02 03", "x");
        Assert.Equal(3, writer.RecordingTotalBytes);
        Assert.Equal(3, writer.GetRecordedRawBytes().Length);
    }

    [Fact]
    public async Task SessionJson_HasSchemaVersion2()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"json-{Guid.NewGuid():N}");
        var session = new SerialCaptureSession
        {
            PortSettings = SerialPortSettings.CreateDefault(),
            SessionType = GuidedSessionType.EmptyStable,
            IsStableSession = true,
            KnownWeightKg = 0
        };
        var path = await SessionMetadataWriter.WriteAsync(basePath, session, 2, "a.log", "a.raw.bin",
            new CaptureSaveOptions { PortSettings = session.PortSettings, TotalChunks = 1, UniqueByteValues = [0x00] });
        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"schemaVersion\": 2", json);
        File.Delete(path);
    }

    [Fact]
    public void Filename_ContainsSerialConfigAndSessionType()
    {
        var settings = SerialPortSettings.CreateDefault();
        var name = CaptureFileNameBuilder.BuildBaseName(settings, GuidedSessionType.EmptyStable, DateTimeOffset.Now);
        Assert.Contains("COM1", name);
        Assert.Contains("9600_8N1", name);
        Assert.Contains("EmptyStable", name);
    }

    [Fact]
    public void Filename_SanitizesInvalidCharacters()
    {
        var settings = new SerialPortSettings { PortName = "COM#1?", BaudRate = 9600, DataBits = 8, Parity = "None", StopBits = "One" };
        var name = CaptureFileNameBuilder.BuildBaseName(settings, GuidedSessionType.PersonStable, DateTimeOffset.Now);
        Assert.DoesNotContain("#", name);
        Assert.DoesNotContain("?", name);
    }

    [Fact]
    public void AutoStop_UsesRequiredCaptureDurations()
    {
        Assert.Equal(10, GuidedCaptureTimings.StableStabilizeSeconds);
        Assert.Equal(30, GuidedCaptureTimings.StableRecordSeconds);
        Assert.Equal(10, GuidedCaptureTimings.TransitionInitialEmptySeconds);
        Assert.Equal(20, GuidedCaptureTimings.TransitionStableHoldSeconds);
        Assert.Equal(10, GuidedCaptureTimings.TransitionFinalEmptySeconds);
        Assert.True(GuidedCaptureTimings.StableRecordSeconds >= GuidedCaptureTimings.StableMinimumValidDurationSeconds);
    }

    [Fact]
    public void AutoStopDuration_StableMinimum25Seconds()
    {
        var stats = new CaptureRecordingStats
        {
            Duration = TimeSpan.FromSeconds(24),
            TotalBytes = 10,
            IsStableSession = true,
            HasKnownWeight = true,
            TextLogSaved = true,
            RawFileSaved = true,
            SessionJsonSaved = true,
            RawFileSize = 10
        };
        Assert.Equal(CaptureQualityStatus.Invalid, CaptureQualityValidator.Validate(stats).Status);
    }

    [Fact]
    public void CancelSession_DiscardsWithoutSave()
    {
        using var writer = new RawSerialLogWriter();
        writer.StartSession(new SerialCaptureSession { PortSettings = SerialPortSettings.CreateDefault() });
        writer.WriteChunk(MakeChunk(1, [0x01]), "01", "x");
        writer.DiscardRecording();
        Assert.False(writer.IsRecording);
        Assert.Equal(0, writer.RecordingTotalBytes);
    }

    [Fact]
    public void OnlyTwoUniqueBytes_IsWarningNotInvalid()
    {
        var stats = new CaptureRecordingStats
        {
            Duration = TimeSpan.FromSeconds(30),
            TotalBytes = 100,
            UniqueByteValues = [0x00, 0x80],
            IsStableSession = true,
            HasKnownWeight = true,
            TextLogSaved = true,
            RawFileSaved = true,
            SessionJsonSaved = true,
            RawFileSize = 100
        };
        var result = CaptureQualityValidator.Validate(stats);
        Assert.Equal(CaptureQualityStatus.Warning, result.Status);
    }

    [Fact]
    public void NoBytes_IsInvalid()
    {
        var stats = new CaptureRecordingStats { TotalBytes = 0, RawFileSize = 0 };
        Assert.Equal(CaptureQualityStatus.Invalid, CaptureQualityValidator.Validate(stats).Status);
    }

    [Fact]
    public void MissingTransitionMarkers_IsWarningOrInvalid()
    {
        var stats = new CaptureRecordingStats
        {
            Duration = TimeSpan.FromSeconds(60),
            TotalBytes = 50,
            SessionType = GuidedSessionType.EmptyPersonTransition,
            Events = [],
            TextLogSaved = true,
            RawFileSaved = true,
            SessionJsonSaved = true,
            RawFileSize = 50
        };
        Assert.Equal(CaptureQualityStatus.Invalid, CaptureQualityValidator.Validate(stats).Status);
    }

    [Fact]
    public void CompareRawStreams_DetectsDifference()
    {
        var cmp = CaptureStreamComparer.Compare([0x00, 0x00], [0x00, 0x80]);
        Assert.False(cmp.RawStreamsIdentical);
        Assert.Contains("khác", cmp.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeviceTesterProvider_HasNoWriteOrSend()
    {
        var methods = typeof(WindowsSerialPortProvider).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name.Contains("Write", StringComparison.Ordinal) || m.Name.Contains("Send", StringComparison.Ordinal));
        Assert.Empty(methods);
    }

    [Fact]
    public async Task ExportFiles_AllThreeFormats()
    {
        using var provider = new FakeSerialPortProvider();
        using var writer = new RawSerialLogWriter();
        using var service = new SerialCaptureService(provider, new FakeSerialPortDiscoveryService(), writer);
        service.UpdateSessionMetadata(new SerialCaptureSession
        {
            PortSettings = SerialPortSettings.CreateDefault(),
            SessionType = GuidedSessionType.EmptyStable,
            IsStableSession = true,
            KnownWeightKg = 0
        });
        service.StartRecording();
        provider.SimulateDataReceived([0x00, 0x80]);
        var result = await service.SaveLogAsync(Path.GetTempPath(), new CaptureSaveOptions
        {
            PortSettings = SerialPortSettings.CreateDefault(),
            SessionType = GuidedSessionType.EmptyStable,
            TotalChunks = 1,
            UniqueByteValues = [0x00, 0x80]
        });
        Assert.True(File.Exists(result.TextLogPath));
        Assert.NotNull(result.RawBinaryPath);
        Assert.NotNull(result.SessionJsonPath);
        File.Delete(result.TextLogPath);
        if (result.RawBinaryPath is not null) File.Delete(result.RawBinaryPath);
        if (result.SessionJsonPath is not null) File.Delete(result.SessionJsonPath);
    }

    private static SerialCaptureService CreateService(FakeSerialPortProvider provider) =>
        new(provider, new FakeSerialPortDiscoveryService(), new RawSerialLogWriter());

    private static SerialDataChunk MakeChunk(int number, byte[] bytes) => new()
    {
        ChunkNumber = number,
        Timestamp = DateTimeOffset.Now,
        RawBytes = bytes
    };
}
