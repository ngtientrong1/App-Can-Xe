namespace CanXe.ScaleProtocol.Core;

public enum ScaleStableSource
{
    Unknown,
    HardwareFlag,
    SoftwareWindow
}

public sealed class ScaleProtocolStatusFlags
{
    public ScaleProtocolStatusFlags(bool? hardwareStable, bool? motion)
    {
        HardwareStable = hardwareStable;
        Motion = motion;
    }

    /// <summary>
    /// When set, protocol code bytes 8-9 indicate stable (observed "01" on production hardware).
    /// </summary>
    public bool? HardwareStable { get; }

    public bool? Motion { get; }
}

public static class ScaleProtocolStatus
{
    /// <summary>
    /// Interprets protocol code bytes 8-9.
    /// Production hardware uses "01" when the physical display is stable.
    /// Second digit '1' = stable, '0' = motion/unstable is a common convention for this family.
    /// </summary>
    public static ScaleProtocolStatusFlags Interpret(string protocolCode)
    {
        if (protocolCode.Length != 2)
            return new ScaleProtocolStatusFlags(null, null);

        if (protocolCode[1] == '1')
            return new ScaleProtocolStatusFlags(true, false);

        if (protocolCode[1] == '0')
            return new ScaleProtocolStatusFlags(false, true);

        return new ScaleProtocolStatusFlags(null, null);
    }
}
