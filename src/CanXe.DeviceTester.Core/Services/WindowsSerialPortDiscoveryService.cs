using System.IO.Ports;

namespace CanXe.DeviceTester.Core;

public sealed class WindowsSerialPortDiscoveryService : ISerialPortDiscoveryService
{
    public IReadOnlyList<string> GetAvailablePortNames() =>
        SerialPort.GetPortNames().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
}
