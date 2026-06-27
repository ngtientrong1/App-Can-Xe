namespace CanXe.ScaleProtocol.Core;

public sealed class ScaleFrameParser
{
    private readonly byte[] _buffer = new byte[ScaleProtocolConstants.MaxParserBufferBytes];
    private int _length;

    public ScaleParserStatistics Statistics { get; } = new();

    public int BufferedByteCount => _length;

    public void ResetBuffer()
    {
        if (_length > 0)
            Statistics.DiscardedBytes += _length;
        _length = 0;
        Statistics.SetBufferedBytes(0);
    }

    public IReadOnlyList<ScaleReading> Append(
        ReadOnlySpan<byte> data,
        DateTimeOffset receivedAt,
        ScaleStabilityDetector stabilityDetector)
    {
        if (data.IsEmpty)
            return [];

        Statistics.TotalBytesFed += data.Length;
        var readings = new List<ScaleReading>();

        foreach (var b in data)
            ProcessByte(b, receivedAt, stabilityDetector, readings);

        Statistics.SetBufferedBytes(_length);
        return readings;
    }

    private void ProcessByte(
        byte b,
        DateTimeOffset receivedAt,
        ScaleStabilityDetector stabilityDetector,
        List<ScaleReading> readings)
    {
        if (_length == 0)
        {
            if (b != ScaleProtocolConstants.Stx)
            {
                Statistics.DiscardedBytes++;
                return;
            }

            _buffer[0] = b;
            _length = 1;
            return;
        }

        if (b == ScaleProtocolConstants.Stx)
        {
            Statistics.InvalidFrames++;
            Statistics.DiscardedBytes += _length;
            _buffer[0] = b;
            _length = 1;
            return;
        }

        if (_length >= ScaleProtocolConstants.MaxParserBufferBytes)
        {
            Statistics.InvalidFrames++;
            Statistics.DiscardedBytes += _length;
            _length = 0;
            if (b == ScaleProtocolConstants.Stx)
            {
                _buffer[0] = b;
                _length = 1;
            }
            else
                Statistics.DiscardedBytes++;

            return;
        }

        _buffer[_length++] = b;

        if (_length < ScaleProtocolConstants.FrameLength)
            return;

        TryCompleteFrame(receivedAt, stabilityDetector, readings);
        _length = 0;
    }

    private void TryCompleteFrame(
        DateTimeOffset receivedAt,
        ScaleStabilityDetector stabilityDetector,
        List<ScaleReading> readings)
    {
        var frameSpan = _buffer.AsSpan(0, ScaleProtocolConstants.FrameLength);
        var validation = ScaleFrameValidator.Validate(frameSpan);
        if (!validation.IsValid || validation.Frame is null)
        {
            Statistics.InvalidFrames++;
            stabilityDetector.NotifyInvalidFrame(receivedAt);
            return;
        }

        Statistics.ValidFrames++;
        var stability = stabilityDetector.NotifyValidReading(validation.Frame, receivedAt);
        readings.Add(ScaleReading.FromValidatedFrame(
            validation.Frame,
            receivedAt,
            stability.IsStable,
            stability.ConsecutiveMatchingFrames));
    }
}
