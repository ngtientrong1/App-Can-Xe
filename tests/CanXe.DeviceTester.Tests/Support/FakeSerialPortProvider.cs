using CanXe.DeviceTester.Core;
using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Tests.Support;

public sealed class FakeSerialPortDiscoveryService : ISerialPortDiscoveryService
{
    public IReadOnlyList<string> Ports { get; set; } = ["COM1", "COM2"];

    public IReadOnlyList<string> GetAvailablePortNames() => Ports;
}

public sealed class FakeSerialPortProvider : ISerialPortProvider
{
    public bool IsOpen { get; private set; }
    public SerialPortSettings? LastOpenSettings { get; private set; }
    public int CloseCount { get; private set; }
    public int BytesWritten { get; private set; }

    public Exception? OpenException { get; set; }
    public Exception? ReadFailure { get; set; }

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<Exception>? ReadFailed;

    public Task OpenAsync(SerialPortSettings settings, CancellationToken cancellationToken = default)
    {
        if (OpenException is not null)
            throw OpenException;
        LastOpenSettings = settings;
        IsOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        CloseCount++;
        IsOpen = false;
        return Task.CompletedTask;
    }

    public void SimulateDataReceived(byte[] data) => DataReceived?.Invoke(this, data);

    public void SimulateReadFailed(Exception ex) => ReadFailed?.Invoke(this, ex);

    public void Dispose() => IsOpen = false;

    public void SimulateWriteAttempt() => BytesWritten++;
}
