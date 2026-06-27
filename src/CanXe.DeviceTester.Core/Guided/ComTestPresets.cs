using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Guided;

public sealed class ComTestPreset
{
    public required string Name { get; init; }
    public required int BaudRate { get; init; }
    public required int DataBits { get; init; }
    public required string Parity { get; init; }
    public required string StopBits { get; init; }
    public required string Handshake { get; init; }
    public bool IsCustom { get; init; }

    public void ApplyTo(SerialPortSettings settings)
    {
        settings.BaudRate = BaudRate;
        settings.DataBits = DataBits;
        settings.Parity = Parity;
        settings.StopBits = StopBits;
        settings.Handshake = Handshake;
    }

    public string SerialConfigLabel => CaptureFileNameBuilder.FormatSerialConfig(BaudRate, DataBits, Parity, StopBits);
}

public static class ComTestPresets
{
    public static IReadOnlyList<ComTestPreset> All { get; } =
    [
        new() { Name = "1200 / 8 / None / 1 / None", BaudRate = 1200, DataBits = 8, Parity = "None", StopBits = "One", Handshake = "None" },
        new() { Name = "9600 / 8 / None / 1 / None", BaudRate = 9600, DataBits = 8, Parity = "None", StopBits = "One", Handshake = "None" },
        new() { Name = "56000 / 8 / None / 1 / None", BaudRate = 56000, DataBits = 8, Parity = "None", StopBits = "One", Handshake = "None" },
        new() { Name = "57600 / 8 / None / 1 / None", BaudRate = 57600, DataBits = 8, Parity = "None", StopBits = "One", Handshake = "None" },
        new() { Name = "9600 / 7 / Even / 1 / None", BaudRate = 9600, DataBits = 7, Parity = "Even", StopBits = "One", Handshake = "None" },
        new() { Name = "4800 / 8 / None / 1 / None", BaudRate = 4800, DataBits = 8, Parity = "None", StopBits = "One", Handshake = "None" },
        new() { Name = "2400 / 8 / None / 1 / None", BaudRate = 2400, DataBits = 8, Parity = "None", StopBits = "One", Handshake = "None" },
        new() { Name = "Custom", BaudRate = 9600, DataBits = 8, Parity = "None", StopBits = "One", Handshake = "None", IsCustom = true }
    ];

    public static ComTestPreset Default => All[0];
}
