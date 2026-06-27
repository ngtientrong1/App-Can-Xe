namespace CanXe.Domain.Entities;

public sealed class ScaleDeviceSettings
{
    public int Id { get; set; } = 1;
    public string DeviceMode { get; set; } = "Simulation";
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 1200;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
    public string? ScaleInputMode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
