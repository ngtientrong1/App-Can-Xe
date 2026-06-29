namespace CanXe.ScaleProtocol.Core;

public enum ScaleConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public sealed class ScaleSerialSettings
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = ScaleProtocolConstants.DefaultBaudRate;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
    public int ReadTimeout { get; set; } = 500;
    public long ScaleDivisionKg { get; set; } = 20;
    public bool VerboseFrameLogging { get; set; }
}
