namespace CanXe.ScaleProtocol.Core;

public static class ScaleFrameFixtures
{
    public static readonly byte[] Frame0Kg =
        [0x02, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x42, 0x03];

    public static readonly byte[] Frame10Kg =
        [0x02, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x31, 0x30, 0x30, 0x31, 0x41, 0x03];

    public static readonly byte[] Frame50Kg =
        [0x02, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x35, 0x30, 0x30, 0x31, 0x45, 0x03];

    public static byte[] BuildFrame(char sign, int weightKg, string protocolCode = "01")
    {
        var digits = Math.Abs(weightKg).ToString("D6");
        if (digits.Length != 6)
            throw new ArgumentOutOfRangeException(nameof(weightKg));

        var frame = new byte[12];
        frame[0] = ScaleProtocolConstants.Stx;
        frame[1] = (byte)sign;
        for (var i = 0; i < 6; i++)
            frame[2 + i] = (byte)digits[i];
        frame[8] = (byte)protocolCode[0];
        frame[9] = (byte)protocolCode[1];
        frame[11] = ScaleProtocolConstants.Etx;
        frame[10] = (byte)ScaleProtocolChecksum.ComputeExpectedChecksum(frame);
        return frame;
    }
}
