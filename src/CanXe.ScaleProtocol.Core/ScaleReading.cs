namespace CanXe.ScaleProtocol.Core;

public sealed class ScaleReading
{
    public long WeightKg { get; init; }
    public char Sign { get; init; }
    public string ProtocolCode { get; init; } = string.Empty;
    public char Checksum { get; init; }
    public char ExpectedChecksum { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public byte[] RawFrame { get; init; } = [];
    public bool IsChecksumValid { get; init; }
    public bool IsStable { get; init; }
    public ScaleStableSource StableSource { get; init; }
    public bool? RawStableFlag { get; init; }
    public bool? RawMotionFlag { get; init; }
    public int ConsecutiveMatchingFrames { get; init; }
    public long Sequence { get; init; }
    public TimeSpan? FrameInterval { get; init; }
    public string? DiagnosticWarning { get; init; }

    public string RawFrameHex => Convert.ToHexString(RawFrame);

    public static ScaleReading FromValidatedFrame(
        ScaleProtocolFrame frame,
        DateTimeOffset receivedAt,
        ScaleStabilityState stability,
        long sequence,
        TimeSpan? frameInterval)
    {
        var magnitude = int.Parse(frame.WeightDigits);
        var weight = frame.Sign == '-' ? -magnitude : magnitude;
        var status = ScaleProtocolStatus.Interpret(frame.ProtocolCode);

        return new ScaleReading
        {
            WeightKg = weight,
            Sign = frame.Sign,
            ProtocolCode = frame.ProtocolCode,
            Checksum = frame.ReceivedChecksum,
            ExpectedChecksum = frame.ExpectedChecksum,
            ReceivedAt = receivedAt,
            RawFrame = frame.RawFrame,
            IsChecksumValid = frame.IsChecksumValid,
            IsStable = stability.IsStable,
            StableSource = stability.StableSource,
            RawStableFlag = stability.RawStableFlag,
            RawMotionFlag = status.Motion,
            ConsecutiveMatchingFrames = stability.ConsecutiveMatchingFrames,
            Sequence = sequence,
            FrameInterval = frameInterval,
            DiagnosticWarning = frame.DiagnosticWarning
        };
    }
}
