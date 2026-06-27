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
    public int ConsecutiveMatchingFrames { get; init; }
    public string? DiagnosticWarning { get; init; }

    public static ScaleReading FromValidatedFrame(
        ScaleProtocolFrame frame,
        DateTimeOffset receivedAt,
        bool isStable,
        int consecutiveMatchingFrames)
    {
        var magnitude = int.Parse(frame.WeightDigits);
        var weight = frame.Sign == '-' ? -magnitude : magnitude;

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
            IsStable = isStable,
            ConsecutiveMatchingFrames = consecutiveMatchingFrames,
            DiagnosticWarning = frame.DiagnosticWarning
        };
    }
}
