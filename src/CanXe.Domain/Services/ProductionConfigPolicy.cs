namespace CanXe.Domain.Services;

public static class ProductionConfigPolicy
{
    public const string HardwareMode = "Hardware";
    public const string SimulationMode = "Simulation";
    public const string TestMode = "Test";

    public static bool IsHardwareMode(string? deviceMode) =>
        string.Equals(deviceMode, HardwareMode, StringComparison.OrdinalIgnoreCase);

    public static bool IsSimulationMode(string? deviceMode) =>
        string.Equals(deviceMode, SimulationMode, StringComparison.OrdinalIgnoreCase);

    public static bool IsTestMode(string? deviceMode) =>
        string.Equals(deviceMode, TestMode, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidProductionConfig(
        string? deviceMode,
        bool developerMode,
        bool showDeveloperPanel,
        string jsonContent)
    {
        if (!IsHardwareMode(deviceMode))
            return false;
        if (developerMode)
            return false;
        if (showDeveloperPanel)
            return false;
        if (jsonContent.Contains("\"Simulation\"", StringComparison.OrdinalIgnoreCase))
            return false;
        if (jsonContent.Contains("-stimeout", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }
}
