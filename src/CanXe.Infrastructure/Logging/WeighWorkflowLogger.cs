using System.Text;

namespace CanXe.Infrastructure.Logging;

public static class WeighWorkflowLogger
{
    private static readonly object Gate = new();

    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs",
            "weigh-workflow.log");

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

            lock (Gate)
            {
                var dir = Path.GetDirectoryName(LogFilePath)!;
                Directory.CreateDirectory(dir);
                File.AppendAllText(LogFilePath, line.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Best-effort diagnostics only.
        }
    }
}
