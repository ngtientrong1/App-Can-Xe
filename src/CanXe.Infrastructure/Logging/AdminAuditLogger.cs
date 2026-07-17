using System.Text;



namespace CanXe.Infrastructure.Logging;



public static class AdminAuditLogger

{

    public static string LogFilePath => CanXeLogPaths.GetLogFile("admin-audit.log");



    public static void Write(

        string action,

        string result,

        string role,

        string? ticketId = null,

        string? reason = null,

        string? note = null)

    {

        try

        {

            var line = new StringBuilder()

                .Append(DateTimeOffset.Now.ToString("O"))

                .Append(" | ")

                .Append(Sanitize(action))

                .Append(" | ")

                .Append(Sanitize(result))

                .Append(" | ")

                .Append(Sanitize(role))

                .Append(" | ")

                .Append(Sanitize(ticketId ?? "-"))

                .Append(" | ")

                .Append(Sanitize(reason ?? "-"))

                .Append(" | ")

                .Append(Sanitize(note ?? "-"))

                .AppendLine();



            SafeLogFileAppend.Append(LogFilePath, line.ToString());

        }

        catch

        {

            // Best-effort audit only.

        }

    }



    private static string Sanitize(string value)

    {

        if (string.IsNullOrWhiteSpace(value))

            return "-";



        // Never allow password-like payloads into the log line.

        var trimmed = value.Trim();

        if (trimmed.Contains("password", StringComparison.OrdinalIgnoreCase) ||

            trimmed.Contains("mật khẩu", StringComparison.OrdinalIgnoreCase) ||

            trimmed.Contains("admin123", StringComparison.OrdinalIgnoreCase))

            return "[redacted]";



        return trimmed.Replace('|', '/');

    }

}


