namespace CanXe.ScaleProtocol.Core;

public static class ScaleProtocolConstants
{
    public const byte Stx = 0x02;
    public const byte Etx = 0x03;
    public const int FrameLength = 12;
    public const int DefaultBaudRate = 1200;
    public const string ObservedProtocolCode = "01";
    public const int StaleTimeoutMilliseconds = 2000;
    public const int MaxParserBufferBytes = 256;
}
