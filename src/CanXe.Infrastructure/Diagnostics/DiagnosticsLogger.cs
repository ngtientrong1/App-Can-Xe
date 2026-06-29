using System.Text;

namespace CanXe.Infrastructure.Diagnostics;

public static class DiagnosticsLogger
{
    private static readonly object Gate = new();

    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs",
            "diagnostics.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                File.AppendAllText(
                    LogFilePath,
                    $"{DateTimeOffset.Now:O} {Sanitize(message)}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    public static string Sanitize(string text) =>
        RtspCredentialSanitizer.Redact(text);
}

public static class ReleaseVerificationLogger
{
    private static readonly object Gate = new();

    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs",
            "release-verification.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                File.AppendAllText(
                    LogFilePath,
                    $"{DateTimeOffset.Now:O} {DiagnosticsLogger.Sanitize(message)}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}

public static class RtspCredentialSanitizer
{
    public static string Redact(string text) =>
        System.Text.RegularExpressions.Regex.Replace(
            text,
            @"rtsp://[^""'\s]+@",
            "rtsp://***@",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
