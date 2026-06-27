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
    void StartSession(SerialCaptureSession session);
    void WriteChunk(SerialDataChunk chunk, string hex, string escapedText);
    void WriteSessionNotes(SerialCaptureSession session);
    Task<string> SaveToFileAsync(string directoryPath, string portName, CancellationToken cancellationToken = default);
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

    event EventHandler? StateChanged;

    IReadOnlyList<string> ScanPorts();
    Task OpenPortAsync(CancellationToken cancellationToken = default);
    Task ClosePortAsync(CancellationToken cancellationToken = default);
    void StartRecording();
    void StopRecording();
    Task<string> SaveLogAsync(string? directoryPath, CancellationToken cancellationToken = default);
    void ClearDisplay();
    void UpdateSessionMetadata(SerialCaptureSession session);
    void UpdateSettings(SerialPortSettings settings);
    void ApplyPendingUiUpdates();
}
