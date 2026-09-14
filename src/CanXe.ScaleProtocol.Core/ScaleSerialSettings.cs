namespace CanXe.ScaleProtocol.Core;

public enum ScaleConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error,

    /// <summary>
    /// Open()/Close() did not return within <see cref="ScaleSerialSettings.OpenCloseTimeoutMs"/> —
    /// most likely a wedged USB-to-serial driver. The reader has abandoned the attempt so future
    /// connect/disconnect calls are not blocked forever; the watchdog keeps retrying on schedule.
    /// </summary>
    Hung
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

    /// <summary>
    /// Hard wall-clock cap on a single Open()/Close() call. SerialPort's native Open()/Close() are
    /// blocking and non-cancelable, so this bounds how long a wedged driver can hold the reader's
    /// connection gate before the attempt is abandoned and a fresh one is allowed to proceed.
    /// </summary>
    public int OpenCloseTimeoutMs { get; set; } = 3000;
}
