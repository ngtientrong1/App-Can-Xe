using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Models;

public sealed class SerialPortSettings
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
    public int ReadTimeout { get; set; } = 500;
    public SerialTextEncodingOption TextEncoding { get; set; } = SerialTextEncodingOption.Ascii;

    public static SerialPortSettings CreateDefault() => new();
}
