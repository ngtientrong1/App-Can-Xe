namespace CanXe.Infrastructure.Scale;

/// <summary>
/// Best-effort recovery for a USB-to-serial adapter whose kernel driver is wedged — the port stays
/// "open" and the app stays responsive, but no more data ever arrives, and a plain Close()/Open()
/// on a fresh <see cref="System.IO.Ports.SerialPort"/> does not help because the problem sits below
/// the .NET object, at the driver/USB level. Only a real device re-enumeration (physical unplug or
/// its OS-level equivalent) clears it.
/// </summary>
public interface ISerialPortResetter
{
    Task<bool> TryResetAsync(string portName, CancellationToken cancellationToken = default);
}

public sealed class NoOpSerialPortResetter : ISerialPortResetter
{
    public Task<bool> TryResetAsync(string portName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
