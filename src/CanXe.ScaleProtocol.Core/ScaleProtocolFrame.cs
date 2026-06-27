using System.Text;

namespace CanXe.ScaleProtocol.Core;

public sealed class ScaleProtocolFrame
{
    public required byte[] RawFrame { get; init; }
    public char Sign { get; init; }
    public string WeightDigits { get; init; } = string.Empty;
    public string ProtocolCode { get; init; } = string.Empty;
    public char ReceivedChecksum { get; init; }
    public char ExpectedChecksum { get; init; }
    public bool IsChecksumValid { get; init; }
    public string? DiagnosticWarning { get; init; }

    public string ToDisplayString() =>
        $"<STX>{(char)Sign}{WeightDigits}{ProtocolCode}{ReceivedChecksum}<ETX>";

    public static ScaleProtocolFrame FromBytes(ReadOnlySpan<byte> frame)
    {
        var copy = frame.ToArray();
        var expected = ScaleProtocolChecksum.ComputeExpectedChecksum(frame);
        var received = ScaleProtocolChecksum.NormalizeHexChar(frame[10]);
        var protocolCode = Encoding.ASCII.GetString(frame.Slice(8, 2));
        string? warning = null;
        if (!string.Equals(protocolCode, ScaleProtocolConstants.ObservedProtocolCode, StringComparison.Ordinal))
            warning = $"Protocol code '{protocolCode}' differs from observed '{ScaleProtocolConstants.ObservedProtocolCode}'.";

        return new ScaleProtocolFrame
        {
            RawFrame = copy,
            Sign = (char)frame[1],
            WeightDigits = Encoding.ASCII.GetString(frame.Slice(2, 6)),
            ProtocolCode = protocolCode,
            ReceivedChecksum = received,
            ExpectedChecksum = expected,
            IsChecksumValid = received == expected,
            DiagnosticWarning = warning
        };
    }
}
