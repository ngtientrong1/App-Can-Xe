using System.IO;

namespace CanXe.Desktop.Services;

public sealed class PrintCommandLogger
{
    private static readonly object Gate = new();
    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CanXe",
        "Logs",
        "print-command.log");

    public void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            var line = $"[{DateTimeOffset.Now:O}] {message}";
            lock (Gate)
                File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // diagnostics only
        }
    }

    public void LogMilestone(string milestone) => Log(milestone);

    public void LogException(string context, Exception ex) =>
        Log($"{context} exceptionType={ex.GetType().Name} message={ex.Message} stack={ex.StackTrace}");
}
