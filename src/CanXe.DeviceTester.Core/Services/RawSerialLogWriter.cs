using System.Globalization;
using System.Text;
using CanXe.DeviceTester.Core.Guided;
using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Services;

public sealed class RawSerialLogWriter : IRawSerialLogWriter
{
    private readonly StringBuilder _buffer = new();
    private readonly RawBinaryCaptureWriter _binaryWriter = new();
    private readonly List<CaptureEventMarker> _eventMarkers = [];
    private readonly HashSet<byte> _uniqueBytes = [];
    private SerialCaptureSession? _session;
    private bool _disposed;
    private int _chunkCount;
    private long _lastChunkNumber;

    public bool IsRecording { get; private set; }
    public bool SaveRawBinary { get; set; } = true;
    public bool SaveSessionJson { get; set; } = true;
    public DateTimeOffset? RecordingStartedAt { get; private set; }
    public DateTimeOffset? RecordingEndedAt { get; private set; }
    public int RecordingTotalBytes => _binaryWriter.TotalBytes;
    public int RecordingChunkCount => _chunkCount;
    public long LastChunkNumber => _lastChunkNumber;
    public IReadOnlyList<byte> RecordingUniqueBytes => _uniqueBytes.OrderBy(b => b).ToList();
    public IReadOnlyList<CaptureEventMarker> EventMarkers => _eventMarkers;

    public void StartSession(SerialCaptureSession session)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.Clear();
        _binaryWriter.Reset();
        _eventMarkers.Clear();
        _uniqueBytes.Clear();
        _chunkCount = 0;
        _lastChunkNumber = 0;
        _session = session;
        RecordingStartedAt = DateTimeOffset.Now;
        RecordingEndedAt = null;
        IsRecording = true;

        _buffer.AppendLine("Session started:");
        _buffer.AppendLine($"Port: {session.PortSettings.PortName}");
        _buffer.AppendLine($"BaudRate: {session.PortSettings.BaudRate}");
        _buffer.AppendLine($"DataBits: {session.PortSettings.DataBits}");
        _buffer.AppendLine($"Parity: {session.PortSettings.Parity}");
        _buffer.AppendLine($"StopBits: {session.PortSettings.StopBits}");
        _buffer.AppendLine($"Handshake: {session.PortSettings.Handshake}");
        _buffer.AppendLine($"Encoding: {session.PortSettings.TextEncoding}");
        _buffer.AppendLine($"Windows version: {session.WindowsVersion}");
        _buffer.AppendLine($"Application version: {session.ApplicationVersion}");
        _buffer.AppendLine($"Session label: {session.SessionLabel}");
        if (session.SessionType is not null)
            _buffer.AppendLine($"Session type: {session.SessionType}");
        if (session.KnownWeightKg is not null)
            _buffer.AppendLine($"Known weight kg: {session.KnownWeightKg}");
        if (session.IsStableSession is not null)
            _buffer.AppendLine($"Is stable session: {session.IsStableSession}");
        if (!string.IsNullOrWhiteSpace(session.ScaleDisplayWeight))
            _buffer.AppendLine($"Scale display weight: {session.ScaleDisplayWeight}");
        if (!string.IsNullOrWhiteSpace(session.VehicleCondition))
            _buffer.AppendLine($"Vehicle condition: {session.VehicleCondition}");
        if (!string.IsNullOrWhiteSpace(session.SessionStartNote))
            _buffer.AppendLine($"Session start note: {session.SessionStartNote}");
        if (!string.IsNullOrWhiteSpace(session.AdditionalNotes))
            _buffer.AppendLine($"Additional notes: {session.AdditionalNotes}");
        _buffer.AppendLine();
    }

    public void WriteSessionNotes(SerialCaptureSession session)
    {
        if (!IsRecording)
            return;

        _session = session;
        _buffer.AppendLine("--- Session notes updated ---");
        _buffer.AppendLine($"Session label: {session.SessionLabel}");
        if (session.KnownWeightKg is not null)
            _buffer.AppendLine($"Known weight kg: {session.KnownWeightKg}");
        if (!string.IsNullOrWhiteSpace(session.ScaleDisplayWeight))
            _buffer.AppendLine($"Scale display weight: {session.ScaleDisplayWeight}");
        if (!string.IsNullOrWhiteSpace(session.VehicleCondition))
            _buffer.AppendLine($"Vehicle condition: {session.VehicleCondition}");
        if (!string.IsNullOrWhiteSpace(session.AdditionalNotes))
            _buffer.AppendLine($"Additional notes: {session.AdditionalNotes}");
        _buffer.AppendLine();
    }

    public void WriteChunk(SerialDataChunk chunk, string hex, string escapedText)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRecording)
            return;

        foreach (var b in chunk.RawBytes)
            _uniqueBytes.Add(b);

        _binaryWriter.Append(chunk.RawBytes);
        _chunkCount++;
        _lastChunkNumber = chunk.ChunkNumber;

        _buffer.AppendLine($"Timestamp: {chunk.Timestamp:yyyy-MM-ddTHH:mm:ss.fff}");
        _buffer.AppendLine($"Chunk number: {chunk.ChunkNumber}");
        _buffer.AppendLine($"Byte count: {chunk.ByteCount}");
        _buffer.AppendLine($"HEX: {hex}");
        _buffer.AppendLine($"Escaped text: {escapedText}");
        _buffer.AppendLine();
    }

    public CaptureEventMarker AddEventMarker(string eventType, string label, double elapsedMs, long rawByteOffset, long chunkNumber)
    {
        var marker = new CaptureEventMarker
        {
            EventType = eventType,
            Label = label,
            Timestamp = DateTimeOffset.Now,
            ElapsedMilliseconds = elapsedMs,
            RawByteOffset = rawByteOffset,
            ChunkNumber = chunkNumber
        };
        _eventMarkers.Add(marker);

        if (IsRecording)
        {
            _buffer.AppendLine("--- Event marker ---");
            _buffer.AppendLine($"Event type: {eventType}");
            _buffer.AppendLine($"Label: {label}");
            _buffer.AppendLine($"Timestamp: {marker.Timestamp:yyyy-MM-ddTHH:mm:ss.fff}");
            _buffer.AppendLine($"Elapsed ms: {elapsedMs:F1}");
            _buffer.AppendLine($"Raw byte offset: {rawByteOffset}");
            _buffer.AppendLine($"Chunk number: {chunkNumber}");
            _buffer.AppendLine();
        }

        return marker;
    }

    public void DiscardRecording()
    {
        _buffer.Clear();
        _binaryWriter.Reset();
        _eventMarkers.Clear();
        _uniqueBytes.Clear();
        _chunkCount = 0;
        _lastChunkNumber = 0;
        RecordingStartedAt = null;
        RecordingEndedAt = null;
        IsRecording = false;
    }

    public byte[] GetRecordedRawBytes() => _binaryWriter.GetBytes();

    public async Task<CaptureSaveResult> SaveToFileAsync(string directoryPath, string portName, CancellationToken cancellationToken = default)
    {
        var settings = _session?.PortSettings ?? SerialPortSettings.CreateDefault();
        settings.PortName = string.IsNullOrWhiteSpace(portName) ? settings.PortName : portName;
        return await SaveToFileAsync(directoryPath, new CaptureSaveOptions
        {
            PortSettings = settings,
            SessionType = _session?.SessionType,
            Events = _eventMarkers,
            TotalChunks = _chunkCount,
            UniqueByteValues = RecordingUniqueBytes
        }, cancellationToken);
    }

    public async Task<CaptureSaveResult> SaveToFileAsync(string directoryPath, CaptureSaveOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Directory.CreateDirectory(directoryPath);

        var timestamp = options.Timestamp ?? DateTimeOffset.Now;
        var baseName = options.SessionType is GuidedSessionType sessionType
            ? CaptureFileNameBuilder.BuildBaseName(options.PortSettings, sessionType, timestamp)
            : BuildLegacyBaseName(directoryPath, options.PortSettings.PortName, timestamp);

        var fullBase = Path.Combine(directoryPath, baseName);
        var textPath = fullBase + ".log";
        await File.WriteAllTextAsync(textPath, _buffer.ToString(), Encoding.UTF8, cancellationToken);

        string? rawPath = null;
        if (SaveRawBinary)
            rawPath = await _binaryWriter.SaveAsync(fullBase, cancellationToken);

        string? jsonPath = null;
        if (SaveSessionJson && _session is not null)
        {
            jsonPath = await SessionMetadataWriter.WriteAsync(
                fullBase,
                _session,
                _binaryWriter.TotalBytes,
                textPath,
                rawPath,
                options,
                cancellationToken);
        }

        return new CaptureSaveResult
        {
            TextLogPath = textPath,
            RawBinaryPath = rawPath,
            SessionJsonPath = jsonPath,
            BaseName = fullBase
        };
    }

    public void StopRecording()
    {
        IsRecording = false;
        RecordingEndedAt = DateTimeOffset.Now;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        IsRecording = false;
    }

    private static string BuildLegacyBaseName(string directoryPath, string portName, DateTimeOffset timestamp)
    {
        var safePort = CaptureFileNameBuilder.SanitizeToken(portName);
        var stamp = timestamp.ToLocalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"CanXe_{safePort}_{stamp}";
    }
}
