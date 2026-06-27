using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CanXe.Desktop;

public static class StartupErrorLogger
{
    public static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs");

    public static string LogFilePath => Path.Combine(LogDirectory, "startup-error.log");

    public static void Write(Exception exception)
    {
        EnsureLogDirectoryExists();
        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Exception: {exception.GetType().FullName}");
        builder.AppendLine($"Message: {exception.Message}");
        builder.AppendLine($"App version: {GetAppVersion()}");
        builder.AppendLine($"Windows: {GetWindowsVersion()}");
        builder.AppendLine(exception.StackTrace);
        if (exception.InnerException is not null)
        {
            builder.AppendLine("Inner exception:");
            builder.AppendLine($"Type: {exception.InnerException.GetType().FullName}");
            builder.AppendLine($"Message: {exception.InnerException.Message}");
            builder.AppendLine(exception.InnerException.StackTrace);
        }

        builder.AppendLine(new string('-', 60));
        File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
    }

    public static void EnsureLogDirectoryExists() => Directory.CreateDirectory(LogDirectory);

    private static string GetAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "unknown";
    }

    private static string GetWindowsVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.OSDescription;

        return Environment.OSVersion.ToString();
    }
}
