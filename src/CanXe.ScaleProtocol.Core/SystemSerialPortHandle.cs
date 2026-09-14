using System.IO.Ports;

namespace CanXe.ScaleProtocol.Core;

/// <summary>Default <see cref="ISerialPortHandle"/> backed by the real System.IO.Ports.SerialPort.</summary>
internal sealed class SystemSerialPortHandle : ISerialPortHandle
{
    private readonly SerialPort _port;

    public SystemSerialPortHandle(ScaleSerialSettings settings)
    {
        _port = new SerialPort
        {
            PortName = settings.PortName,
            BaudRate = settings.BaudRate,
            DataBits = settings.DataBits,
            Parity = ParseParity(settings.Parity),
            StopBits = ParseStopBits(settings.StopBits),
            Handshake = ParseHandshake(settings.Handshake),
            ReadTimeout = settings.ReadTimeout,
            WriteTimeout = settings.ReadTimeout,
            DtrEnable = false,
            RtsEnable = false
        };
        _port.DataReceived += (_, _) => DataAvailable?.Invoke(this, EventArgs.Empty);
        _port.ErrorReceived += (_, e) => SerialErrorOccurred?.Invoke(this, e.EventType.ToString());
    }

    public bool IsOpen => _port.IsOpen;
    public int BytesToRead => _port.BytesToRead;

    public void Poke()
    {
        try
        {
            _ = _port.CtsHolding;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Best effort — a poke that fails to reach a dying port is exactly what the
            // stale/hang watchdogs are already there to catch; this must never throw on its own.
        }
    }

    public void Open() => _port.Open();
    public void Close() => _port.Close();
    public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
    public void Dispose() => _port.Dispose();

    public event EventHandler? DataAvailable;
    public event EventHandler<string>? SerialErrorOccurred;

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var parsed) ? parsed : Parity.None;

    private static StopBits ParseStopBits(string value) =>
        Enum.TryParse<StopBits>(value, true, out var parsed) ? parsed : StopBits.One;

    private static Handshake ParseHandshake(string value) =>
        Enum.TryParse<Handshake>(value, true, out var parsed) ? parsed : Handshake.None;
}

public sealed class SystemSerialPortFactory : ISerialPortFactory
{
    public ISerialPortHandle Create(ScaleSerialSettings settings) => new SystemSerialPortHandle(settings);
}
