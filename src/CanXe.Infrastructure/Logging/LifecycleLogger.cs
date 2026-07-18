namespace CanXe.Infrastructure.Logging;

/// <summary>
/// Best-effort app lifecycle milestones for diagnosing shutdown/reopen hangs.
/// Never throws — logging failures must not crash the app.
/// </summary>
public static class LifecycleLogger
{
    private static readonly object Sync = new();
    private static string? _overridePath;

    public static string LogPath =>
        _overridePath ?? CanXeLogPaths.GetLogFile("lifecycle.log");

    public static void SetOverridePath(string? path) => _overridePath = path;

    public static void ClearOverridePath() => _overridePath = null;

    public static void Write(string milestone, string? detail = null)
    {
        try
        {
            var line = detail is null
                ? $"{DateTimeOffset.Now:O}\t{milestone}"
                : $"{DateTimeOffset.Now:O}\t{milestone}\t{detail}";
            SafeLogFileAppend.AppendLine(LogPath, line);
        }
        catch
        {
            // Lifecycle logging must never crash the app.
        }
    }

    public static string ReadAllTextSafe()
    {
        try
        {
            lock (Sync)
            {
                var path = LogPath;
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}
