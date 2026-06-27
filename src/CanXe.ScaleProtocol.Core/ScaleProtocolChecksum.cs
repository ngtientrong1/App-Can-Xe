namespace CanXe.ScaleProtocol.Core;

public static class ScaleProtocolChecksum
{
    public static char ComputeExpectedChecksum(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < ScaleProtocolConstants.FrameLength)
            throw new ArgumentException("Frame must be 12 bytes.", nameof(frame));

        byte xor = 0;
        for (var i = 0; i <= 9; i++)
            xor ^= frame[i];
        xor ^= frame[11];

        var nibble = xor & 0x0F;
        return nibble < 10
            ? (char)('0' + nibble)
            : (char)('A' + nibble - 10);
    }

    public static bool IsChecksumValid(ReadOnlySpan<byte> frame) =>
        NormalizeHexChar(frame[10]) == ComputeExpectedChecksum(frame);

    public static char NormalizeHexChar(byte value) =>
        value switch
        {
            >= (byte)'a' and <= (byte)'f' => (char)(value - 32),
            _ => (char)value
        };
}
