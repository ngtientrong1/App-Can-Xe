using System.Text;

namespace CanXe.ScaleProtocol.Core;

public static class ScaleDiagnosticsLogger
{
    private static readonly object Gate = new();
    private static bool _verboseFramesEnabled;

    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs",
            "scale-frames.log");

    public static bool VerboseFramesEnabled
    {
        get => Volatile.Read(ref _verboseFramesEnabled);
        set => Volatile.Write(ref _verboseFramesEnabled, value);
    }

    public static void LogFrame(ScaleReading reading, TimeSpan? frameInterval)
    {
        if (!VerboseFramesEnabled)
            return;

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {reading.ReceivedAt:O}");
            builder.AppendLine($"RawFrameHex: {reading.RawFrameHex}");
            builder.AppendLine($"ParsedWeight: {reading.WeightKg}");
            builder.AppendLine($"ParsedStableFlag: {reading.RawStableFlag}");
            builder.AppendLine($"ParsedMotionFlag: {reading.RawMotionFlag}");
            builder.AppendLine($"ParsedSign: {reading.Sign}");
            builder.AppendLine($"StableSource: {reading.StableSource}");
            builder.AppendLine($"IsStable: {reading.IsStable}");
            builder.AppendLine($"FrameTimestamp: {reading.ReceivedAt:O}");
            builder.AppendLine($"FrameInterval: {frameInterval?.TotalMilliseconds.ToString("F1") ?? "—"}ms");
            builder.AppendLine($"Sequence: {reading.Sequence}");
            builder.AppendLine(new string('-', 40));

            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Best effort only.
        }
    }
}
