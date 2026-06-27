using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core;

public interface ISerialPortProvider : IDisposable
{
    bool IsOpen { get; }
    event EventHandler<byte[]>? DataReceived;
    event EventHandler<Exception>? ReadFailed;
    Task OpenAsync(SerialPortSettings settings, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}

public interface ISerialPortDiscoveryService
{
    IReadOnlyList<string> GetAvailablePortNames();
}

public interface IRawSerialLogWriter : IDisposable
{
    bool IsRecording { get; }
    bool SaveRawBinary { get; set; }
    bool SaveSessionJson { get; set; }
    DateTimeOffset? RecordingStartedAt { get; }
    DateTimeOffset? RecordingEndedAt { get; }
    int RecordingTotalBytes { get; }
    int RecordingChunkCount { get; }
    long LastChunkNumber { get; }
    IReadOnlyList<byte> RecordingUniqueBytes { get; }
    IReadOnlyList<CaptureEventMarker> EventMarkers { get; }
    byte[] GetRecordedRawBytes();
    void StartSession(SerialCaptureSession session);
    void WriteChunk(SerialDataChunk chunk, string hex, string escapedText);
    void WriteSessionNotes(SerialCaptureSession session);
    CaptureEventMarker AddEventMarker(string eventType, string label, double elapsedMs, long rawByteOffset, long chunkNumber);
    void DiscardRecording();
    Task<CaptureSaveResult> SaveToFileAsync(string directoryPath, string portName, CancellationToken cancellationToken = default);
    Task<CaptureSaveResult> SaveToFileAsync(string directoryPath, CaptureSaveOptions options, CancellationToken cancellationToken = default);
    void StopRecording();
}

public interface ISerialCaptureService : IDisposable
{
    SerialConnectionStatus Status { get; }
    SerialPortSettings Settings { get; }
    bool IsPortOpen { get; }
    bool IsRecording { get; }
    bool IsDisplayPaused { get; set; }
    string? StatusMessage { get; }
    int TotalBytesReceived { get; }
    int DataReceivedCount { get; }
    DateTimeOffset? LastDataReceivedAt { get; }
    double BytesPerSecondRecent { get; }
    TimeSpan SessionElapsed { get; }
    IReadOnlyList<SerialDataChunk> DisplayChunks { get; }
    byte[] AccumulatedBuffer { get; }
    string LatestHexLine { get; }
    string LatestEscapedText { get; }
    string EventChunksText { get; }
    SerialCaptureSession? CurrentSession { get; }
    DateTimeOffset? RecordingStartedAt { get; }
    DateTimeOffset? RecordingEndedAt { get; }
    int RecordingTotalBytes { get; }
    int RecordingChunkCount { get; }
    long LastChunkNumber { get; }
    IReadOnlyList<byte> RecordingUniqueBytes { get; }
    IReadOnlyList<CaptureEventMarker> EventMarkers { get; }
    double RecordingLongestNoDataGapMs { get; }
    bool PortErrorDuringRecording { get; }

    event EventHandler? StateChanged;
    event EventHandler<byte[]>? RawBytesReceived;

    IReadOnlyList<string> ScanPorts();
    Task OpenPortAsync(CancellationToken cancellationToken = default);
    Task ClosePortAsync(CancellationToken cancellationToken = default);
    void StartRecording();
    void StopRecording();
    void DiscardRecording();
    bool SaveRawBinary { get; set; }
    bool SaveSessionJson { get; set; }
    Task<CaptureSaveResult> SaveLogAsync(string? directoryPath, CancellationToken cancellationToken = default);
    Task<CaptureSaveResult> SaveLogAsync(string? directoryPath, CaptureSaveOptions options, CancellationToken cancellationToken = default);
    CaptureEventMarker AddEventMarker(string eventType, string label);
    void ClearDisplay();
    void UpdateSessionMetadata(SerialCaptureSession session);
    void UpdateSettings(SerialPortSettings settings);
    void ApplyPendingUiUpdates();
    byte[] GetRecordedRawBytes();
}
