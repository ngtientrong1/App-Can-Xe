namespace CanXe.ScaleProtocol.Core;

public static class ScaleFrameValidator
{
    public static ScaleFrameValidationResult Validate(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != ScaleProtocolConstants.FrameLength)
            return ScaleFrameValidationResult.Invalid("length");

        if (frame[0] != ScaleProtocolConstants.Stx)
            return ScaleFrameValidationResult.Invalid("missing_stx");

        if (frame[11] != ScaleProtocolConstants.Etx)
            return ScaleFrameValidationResult.Invalid("missing_etx");

        if (frame[1] is not ((byte)'+' or (byte)'-'))
            return ScaleFrameValidationResult.Invalid("invalid_sign");

        for (var i = 2; i <= 7; i++)
        {
            if (frame[i] is < (byte)'0' or > (byte)'9')
                return ScaleFrameValidationResult.Invalid("invalid_weight_digit");
        }

        for (var i = 8; i <= 9; i++)
        {
            if (frame[i] is < (byte)'0' or > (byte)'9')
                return ScaleFrameValidationResult.Invalid("invalid_protocol_digit");
        }

        var checksum = frame[10];
        if (!IsHexChar(checksum))
            return ScaleFrameValidationResult.Invalid("invalid_checksum_char");

        var parsed = ScaleProtocolFrame.FromBytes(frame);
        if (!parsed.IsChecksumValid)
            return ScaleFrameValidationResult.Invalid("checksum_mismatch");

        return ScaleFrameValidationResult.Valid(parsed);
    }

    private static bool IsHexChar(byte value) =>
        value is >= (byte)'0' and <= (byte)'9'
            or >= (byte)'A' and <= (byte)'F'
            or >= (byte)'a' and <= (byte)'f';
}
