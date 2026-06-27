namespace CanXe.Application.Configuration;

public sealed class AppSettings
{
    public string DeviceMode { get; set; } = "Simulation";
    public int PhotoRetentionDays { get; set; } = 3;
    public ScaleSettings Scale { get; set; } = new();
    public bool CameraPreviewEnabled { get; set; } = true;
    public bool SimulateCameraFailure { get; set; }
    public bool SimulateScaleDisconnect { get; set; }
    public bool ShowDeveloperPanel { get; set; } = true;
    public bool DeveloperTicketEditEnabled { get; set; } = true;
}

public sealed class ScaleSettings
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
}
