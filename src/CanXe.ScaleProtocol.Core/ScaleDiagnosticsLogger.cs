using System.Collections.Concurrent;
using System.Text;

namespace CanXe.ScaleProtocol.Core;

public static class ScaleDiagnosticsLogger
{
    private static bool _verboseFramesEnabled;

    // Mirrors CanXeLogPaths/SafeLogFileAppend (Infrastructure); this assembly cannot reference Infrastructure.
    private static string? _overrideRoot;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> AppendLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public static string LogFilePath => GetLogFile("scale-frames.log");

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

            SafeAppend(LogFilePath, builder.ToString());
        }
        catch
        {
            // Best effort only.
        }
    }

    private static string GetLogFile(string fileName) => Path.Combine(ResolveLogsDirectory(), fileName);

    private static string ResolveLogsDirectory()
    {
        var env = Environment.GetEnvironmentVariable("CANXE_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        if (!string.IsNullOrWhiteSpace(_overrideRoot))
            return _overrideRoot;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs");
    }

    private static void SafeAppend(string path, string text)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var semaphore = AppendLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        semaphore.Wait();
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var stream = new FileStream(
                        path,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var writer = new StreamWriter(stream, Encoding.UTF8);
                    writer.Write(text);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(50 + attempt * 50);
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
}
