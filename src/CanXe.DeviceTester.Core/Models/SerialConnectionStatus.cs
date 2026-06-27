namespace CanXe.DeviceTester.Core.Models;

public enum SerialConnectionStatus
{
    Closed,
    Connecting,
    Open,
    NoData,
    Receiving,
    PortBusy,
    Disconnected,
    ReadError
}

public enum SerialTextEncodingOption
{
    Ascii,
    Utf8,
    Windows1258
}
