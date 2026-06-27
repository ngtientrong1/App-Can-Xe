using System.IO.Ports;
using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Services;

public sealed class WindowsSerialPortProvider : ISerialPortProvider
{
    private SerialPort? _port;
    private readonly object _sync = new();

    public bool IsOpen => _port?.IsOpen == true;

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<Exception>? ReadFailed;

    public Task OpenAsync(SerialPortSettings settings, CancellationToken cancellationToken = default)
    {
        if (IsOpen)
            throw new InvalidOperationException("Cổng đã được mở.");

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

        _port.DataReceived += OnPortDataReceived;

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _port.Open();
        }, cancellationToken);
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_sync)
            {
                if (_port is null)
                    return;

                _port.DataReceived -= OnPortDataReceived;
                if (_port.IsOpen)
                    _port.Close();
                _port.Dispose();
                _port = null;
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        try
        {
            CloseAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort on shutdown.
        }
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        lock (_sync)
        {
            if (_port is null || !_port.IsOpen)
                return;

            try
            {
                var count = _port.BytesToRead;
                if (count <= 0)
                    return;

                var buffer = new byte[count];
                var read = _port.Read(buffer, 0, count);
                if (read <= 0)
                    return;

                if (read != buffer.Length)
                    Array.Resize(ref buffer, read);

                DataReceived?.Invoke(this, buffer);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                ReadFailed?.Invoke(this, ex);
            }
        }
    }

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var parsed) ? parsed : Parity.None;

    private static StopBits ParseStopBits(string value) =>
        Enum.TryParse<StopBits>(value, true, out var parsed) ? parsed : StopBits.One;

    private static Handshake ParseHandshake(string value) =>
        Enum.TryParse<Handshake>(value, true, out var parsed) ? parsed : Handshake.None;
}
