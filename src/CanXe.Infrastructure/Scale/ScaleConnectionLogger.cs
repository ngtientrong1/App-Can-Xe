using System.Text;

namespace CanXe.Infrastructure.Scale;

public static class ScaleConnectionLogger
{
    private static readonly object Gate = new();

    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs",
            "scale-connection.log");

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

            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                File.AppendAllText(LogFilePath, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
