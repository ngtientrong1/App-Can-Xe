using System.Text;

namespace CanXe.Infrastructure.Logging;

public static class WeighWorkflowLogger
{
    public static string LogFilePath => CanXeLogPaths.GetLogFile("weigh-workflow.log");

    public static void Write(string milestone, string? detail = null)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" [")
                .Append(milestone)
                .Append(']');

            if (!string.IsNullOrWhiteSpace(detail))
                line.Append(' ').Append(detail);

            line.AppendLine();

            SafeLogFileAppend.Append(LogFilePath, line.ToString());
        }
        catch
        {
            // Best-effort diagnostics only.
        }
    }
}
