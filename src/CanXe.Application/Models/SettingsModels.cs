namespace CanXe.Application.Models;

public sealed class StationSettingsDto
{
    public string StationName { get; set; } = "Trạm cân CanXe";
    public string? OwnerName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxCode { get; set; }
    public string? LogoPath { get; set; }
    public string? TicketFooterText { get; set; }
}

public sealed class ScaleDeviceSettingsDto
{
    public string DeviceMode { get; set; } = "Simulation";
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
}

public sealed class CameraDeviceSettingsDto
{
    public string? CameraName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? RtspUrl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool PreviewEnabled { get; set; } = true;
    public bool AutoConnectionCheck { get; set; } = true;
    public int SnapshotTimeoutSeconds { get; set; } = 5;
    public int PhotoRetentionDays { get; set; } = 3;
}

public sealed class SettingsValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public Dictionary<string, string> Errors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddError(string field, string message) => Errors[field] = message;
}

public sealed class ConnectionTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ConnectionTestResult Succeeded(string message) =>
        new() { Success = true, Message = message };

    public static ConnectionTestResult Failed(string message) =>
        new() { Success = false, Message = message };
}

public enum AppNavigationSection
{
    System,
    WeighTicket,
    Catalog,
    Device,
    Report,
    Settings
}
