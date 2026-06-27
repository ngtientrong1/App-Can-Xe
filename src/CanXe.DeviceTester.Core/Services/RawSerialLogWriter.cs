using System.Globalization;
using System.Text;
using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Services;

public sealed class RawSerialLogWriter : IRawSerialLogWriter
{
    private readonly StringBuilder _buffer = new();
    private bool _disposed;

    public bool IsRecording { get; private set; }

    public void StartSession(SerialCaptureSession session)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.Clear();
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

        _buffer.AppendLine("--- Session notes updated ---");
        _buffer.AppendLine($"Session label: {session.SessionLabel}");
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

        _buffer.AppendLine($"Timestamp: {chunk.Timestamp:yyyy-MM-ddTHH:mm:ss.fff}");
        _buffer.AppendLine($"Chunk number: {chunk.ChunkNumber}");
        _buffer.AppendLine($"Byte count: {chunk.ByteCount}");
        _buffer.AppendLine($"HEX: {hex}");
        _buffer.AppendLine($"Escaped text: {escapedText}");
        _buffer.AppendLine();
    }

    public async Task<string> SaveToFileAsync(string directoryPath, string portName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Directory.CreateDirectory(directoryPath);
        var safePort = string.Concat(portName.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(safePort))
            safePort = "COM";

        var fileName = $"CanXe_{safePort}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        var fullPath = Path.Combine(directoryPath, fileName);
        await File.WriteAllTextAsync(fullPath, _buffer.ToString(), Encoding.UTF8, cancellationToken);
        return fullPath;
    }

    public void StopRecording() => IsRecording = false;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        IsRecording = false;
    }
}
