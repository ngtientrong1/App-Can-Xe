using System.Globalization;
using System.Text;
using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Guided;

public static class CaptureFileNameBuilder
{
    public static string BuildBaseName(SerialPortSettings settings, GuidedSessionType sessionType, DateTimeOffset timestamp)
    {
        var port = SanitizeToken(settings.PortName);
        var serial = FormatSerialConfig(settings.BaudRate, settings.DataBits, settings.Parity, settings.StopBits);
        var type = sessionType.ToString();
        var stamp = timestamp.ToLocalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"CanXe_{port}_{serial}_{type}_{stamp}";
    }

    public static string FormatSerialConfig(int baudRate, int dataBits, string parity, string stopBits)
    {
        var parityCode = parity switch
        {
            "None" => "N",
            "Even" => "E",
            "Odd" => "O",
            "Mark" => "M",
            "Space" => "S",
            _ => SanitizeToken(parity)
        };

        var stopCode = stopBits switch
        {
            "One" => "1",
            "Two" => "2",
            "OnePointFive" => "1.5",
            _ => SanitizeToken(stopBits)
        };

        return $"{baudRate}_{dataBits}{parityCode}{stopCode}";
    }

    public static string SanitizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unknown";

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '-')
                sb.Append(ch);
        }

        var result = sb.ToString();
        return string.IsNullOrWhiteSpace(result) ? "Unknown" : result;
    }
}
