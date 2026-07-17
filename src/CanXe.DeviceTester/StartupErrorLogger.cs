using System.IO;
using System.Text;

namespace CanXe.DeviceTester;

public static class StartupErrorLogger
{
    public static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXeDeviceTester",
            "Logs");

    public static string LogFilePath => Path.Combine(LogDirectory, "startup-error.log");

    public static void Write(Exception exception)
    {
        Directory.CreateDirectory(LogDirectory);
        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Exception: {exception.GetType().FullName}");
        builder.AppendLine($"Message: {exception.Message}");
        builder.AppendLine(exception.StackTrace);
        if (exception.InnerException is not null)
        {
            builder.AppendLine("Inner exception:");
            builder.AppendLine(exception.InnerException.ToString());
        }

        builder.AppendLine(new string('-', 60));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var stream = new FileStream(
                    LogFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.Write(builder.ToString());
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50 + attempt * 50);
            }
        }
    }

    public static void EnsureLogDirectoryExists() => Directory.CreateDirectory(LogDirectory);
}
