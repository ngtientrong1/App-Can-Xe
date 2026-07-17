using System.Text;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Diagnostics;

public static class DiagnosticsLogger
{
    public static string LogFilePath => CanXeLogPaths.GetLogFile("diagnostics.log");

    public static void Write(string message)
    {
        try
        {
            SafeLogFileAppend.AppendLine(
                LogFilePath,
                $"{DateTimeOffset.Now:O} {Sanitize(message)}");
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
    public static string LogFilePath => CanXeLogPaths.GetLogFile("release-verification.log");

    public static void Write(string message)
    {
        try
        {
            SafeLogFileAppend.AppendLine(
                LogFilePath,
                $"{DateTimeOffset.Now:O} {DiagnosticsLogger.Sanitize(message)}");
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
