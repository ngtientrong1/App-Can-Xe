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
        File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
    }

    public static void EnsureLogDirectoryExists() => Directory.CreateDirectory(LogDirectory);
}
