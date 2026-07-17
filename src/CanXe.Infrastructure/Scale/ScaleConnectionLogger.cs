using System.Text;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Scale;

public static class ScaleConnectionLogger
{
    public static string LogFilePath => CanXeLogPaths.GetLogFile("scale-connection.log");

    public static void Write(string eventName, string? detail = null, string? port = null)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
            sb.AppendLine($"Event: {eventName}");
            if (!string.IsNullOrWhiteSpace(port))
                sb.AppendLine($"Port: {port}");
            if (!string.IsNullOrWhiteSpace(detail))
                sb.AppendLine($"Detail: {detail}");
            sb.AppendLine(new string('-', 40));

            SafeLogFileAppend.Append(LogFilePath, sb.ToString());
        }
        catch
        {
            // Best effort.
        }
    }
}
