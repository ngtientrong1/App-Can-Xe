namespace CanXe.DeviceTester.Core.Models;

public sealed class SerialDataChunk
{
    public required int ChunkNumber { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required byte[] RawBytes { get; init; }
    public int ByteCount => RawBytes.Length;
}
