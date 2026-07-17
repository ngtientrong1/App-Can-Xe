using System.IO;
using System.Text;
using CanXe.Infrastructure.Logging;

namespace CanXe.Desktop.Services;

public static class ScaleConnectionLogger
{
    public static string LogFilePath => CanXeLogPaths.GetLogFile("scale-connection.log");

    public static void Write(
        int operationId,
        string eventName,
        bool portOpenBefore,
        string? settingsSummary,
        bool? connectSucceeded,
        int? retryNumber,
        string activeMode,
        bool developerMode,
        string? detail = null)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Event: {eventName}");
            builder.AppendLine($"OperationId: {operationId}");
            builder.AppendLine($"Thread: {Environment.CurrentManagedThreadId}");
            builder.AppendLine($"Port open before: {(portOpenBefore ? "yes" : "no")}");
            if (settingsSummary is not null)
                builder.AppendLine($"Settings: {settingsSummary}");
            if (connectSucceeded.HasValue)
                builder.AppendLine($"Connect result: {(connectSucceeded.Value ? "succeeded" : "failed")}");
            if (retryNumber.HasValue)
                builder.AppendLine($"Retry number: {retryNumber.Value}");
            builder.AppendLine($"Active mode: {activeMode}");
            builder.AppendLine($"Developer mode: {(developerMode ? "yes" : "no")}");
            if (!string.IsNullOrWhiteSpace(detail))
                builder.AppendLine($"Detail: {detail}");
            builder.AppendLine(new string('-', 60));
            SafeLogFileAppend.Append(LogFilePath, builder.ToString());
        }
        catch
        {
            // Best effort.
        }
    }
}
