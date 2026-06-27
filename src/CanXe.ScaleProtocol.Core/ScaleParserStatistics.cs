namespace CanXe.ScaleProtocol.Core;

public sealed class ScaleParserStatistics
{
    public long TotalBytesFed { get; set; }
    public long DiscardedBytes { get; set; }
    public int ValidFrames { get; set; }
    public int InvalidFrames { get; set; }
    public int BufferedBytes => _bufferedBytes;

    private int _bufferedBytes;

    internal void SetBufferedBytes(int count) => _bufferedBytes = count;
}
