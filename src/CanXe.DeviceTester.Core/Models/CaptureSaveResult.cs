namespace CanXe.DeviceTester.Core.Models;

public sealed class CaptureSaveResult
{
    public required string TextLogPath { get; init; }
    public string? RawBinaryPath { get; init; }
    public string? SessionJsonPath { get; init; }
    public string? BaseName { get; init; }
}

public sealed class SessionMetadataDocument
{
    public int SchemaVersion { get; set; } = 2;
    public string? PortName { get; set; }
    public int BaudRate { get; set; }
    public int DataBits { get; set; }
    public string? Parity { get; set; }
    public string? StopBits { get; set; }
    public string? Handshake { get; set; }
    public string? Encoding { get; set; }
    public string? SessionType { get; set; }
    public string? SessionLabel { get; set; }
    public decimal? KnownWeightKg { get; set; }
    public bool KnownWeightIsApproximate { get; set; }
    public bool? IsStableSession { get; set; }
    public string? StartedAt { get; set; }
    public string? EndedAt { get; set; }
    public long DurationMilliseconds { get; set; }
    public int TotalBytes { get; set; }
    public int TotalChunks { get; set; }
    public List<string> UniqueByteValues { get; set; } = [];
    public string? TextLogFile { get; set; }
    public string? RawBinaryFile { get; set; }
    public List<CaptureEventMarkerDto> Events { get; set; } = [];
}

public sealed class CaptureEventMarkerDto
{
    public string? EventType { get; set; }
    public string? Label { get; set; }
    public string? Timestamp { get; set; }
    public double ElapsedMilliseconds { get; set; }
    public long RawByteOffset { get; set; }
    public long ChunkNumber { get; set; }
}
