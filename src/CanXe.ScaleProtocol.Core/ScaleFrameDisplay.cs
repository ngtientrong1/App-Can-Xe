using System.Text;

namespace CanXe.ScaleProtocol.Core;

public static class ScaleFrameDisplay
{
    public static string FormatPayload(byte[] rawFrame)
    {
        if (rawFrame.Length < 11)
            return "—";

        return Encoding.ASCII.GetString(rawFrame, 1, Math.Min(10, rawFrame.Length - 2));
    }

    public static string? FormatLatestFrame(ScaleReading? reading) =>
        reading is null ? null : FormatPayload(reading.RawFrame);
}
