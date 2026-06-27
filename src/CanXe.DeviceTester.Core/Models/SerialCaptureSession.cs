namespace CanXe.DeviceTester.Core.Models;

public sealed class SerialCaptureSession
{
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public SerialPortSettings PortSettings { get; set; } = SerialPortSettings.CreateDefault();
    public string SessionLabel { get; set; } = SessionLabelPresets.NoVehicle;
    public string? ScaleDisplayWeight { get; set; }
    public string? VehicleCondition { get; set; }
    public string? SessionStartNote { get; set; }
    public string? AdditionalNotes { get; set; }
    public string ApplicationVersion { get; set; } = "2.0.0";
    public string WindowsVersion { get; set; } = Environment.OSVersion.VersionString;
}

public static class SessionLabelPresets
{
    public const string NoVehicle = "Không có xe";
    public const string EmptyVehicle = "Xe trống";
    public const string LoadedVehicle = "Xe có hàng";
    public const string StableWeight = "Trọng lượng ổn định";
    public const string ChangingWeight = "Trọng lượng đang thay đổi";
    public const string OtherTest = "Test khác";

    public static readonly IReadOnlyList<string> All =
    [
        NoVehicle,
        EmptyVehicle,
        LoadedVehicle,
        StableWeight,
        ChangingWeight,
        OtherTest
    ];
}
