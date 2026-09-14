namespace CanXe.ScaleProtocol.Core;

/// <summary>
/// Thin seam over the physical serial port so <see cref="WindowsScaleSerialReader"/>'s hang/timeout
/// handling can be unit tested with a fake that blocks Open()/Close() on command, without needing
/// real hardware or relying on a real port failing fast.
/// </summary>
public interface ISerialPortHandle : IDisposable
{
    bool IsOpen { get; }
    int BytesToRead { get; }

    /// <summary>
    /// Queries the modem control-line status (CTS/DSR/etc.) — a real round-trip to the device
    /// driver that touches nothing on the actual RS-232 data line. Called periodically while
    /// connected purely to generate USB bus activity, so a USB-to-serial adapter that looks idle
    /// to Windows is never selected for USB Selective Suspend. Never throws.
    /// </summary>
    void Poke();

    /// <summary>Blocking call — mirrors System.IO.Ports.SerialPort.Open(), which has no cancellation.</summary>
    void Open();

    /// <summary>Blocking call — mirrors System.IO.Ports.SerialPort.Close(), which has no cancellation.</summary>
    void Close();

    int Read(byte[] buffer, int offset, int count);

    event EventHandler? DataAvailable;
    event EventHandler<string>? SerialErrorOccurred;
}

public interface ISerialPortFactory
{
    ISerialPortHandle Create(ScaleSerialSettings settings);
}
