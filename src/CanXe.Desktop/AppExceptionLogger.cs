using System.IO;
using System.Text;
using CanXe.Infrastructure.Logging;

namespace CanXe.Desktop;

public static class AppExceptionLogger
{
    private static readonly object Gate = new();
    private static int _saveErrorHandledFlag;

    public static string LogDirectory => StartupErrorLogger.LogDirectory;

    public static string AppLogPath => CanXeLogPaths.GetLogFile("app.log");

    public static string ErrorsLogPath => CanXeLogPaths.GetLogFile("errors.log");

    public static void WriteError(string action, Exception exception, string? context = null)
    {
        var builder = new StringBuilder()
            .AppendLine($"Timestamp: {DateTimeOffset.Now:O}")
            .AppendLine($"Action: {action}");

        if (!string.IsNullOrWhiteSpace(context))
            builder.AppendLine($"Context: {context}");

        builder.AppendLine($"Exception: {exception.GetType().FullName}")
            .AppendLine($"Message: {exception.Message}")
            .AppendLine(exception.StackTrace);

        if (exception.InnerException is not null)
        {
            builder.AppendLine("Inner exception:")
                .AppendLine($"Type: {exception.InnerException.GetType().FullName}")
                .AppendLine($"Message: {exception.InnerException.Message}")
                .AppendLine(exception.InnerException.StackTrace);
        }

        builder.AppendLine(new string('-', 60));
        WriteLine(ErrorsLogPath, builder.ToString());
        WriteApp($"ERROR action={action} type={exception.GetType().Name} message={exception.Message}");
    }

    public static void MarkSaveErrorHandled() =>
        Interlocked.Exchange(ref _saveErrorHandledFlag, 1);

    public static bool ConsumeSaveErrorHandled() =>
        Interlocked.Exchange(ref _saveErrorHandledFlag, 0) == 1;

    public static void WriteApp(string message)
    {
        WriteLine(AppLogPath, $"{DateTimeOffset.Now:O} {message}");
    }

    private static void WriteLine(string path, string text)
    {
        try
        {
            lock (Gate)
            {
                StartupErrorLogger.EnsureLogDirectoryExists();
                SafeLogFileAppend.Append(path, text);
                if (!text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                    SafeLogFileAppend.Append(path, Environment.NewLine);
            }
        }
        catch
        {
            // Best-effort diagnostics only.
        }
    }
}
