using CanXe.Infrastructure.Logging;

namespace CanXe.Desktop.Services;

public sealed class PrintCommandLogger
{
    private static string LogPath => CanXeLogPaths.GetLogFile("print-command.log");

    public void Log(string message)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:O}] {message}";
            SafeLogFileAppend.AppendLine(LogPath, line);
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
