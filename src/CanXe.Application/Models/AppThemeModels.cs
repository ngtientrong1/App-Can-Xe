namespace CanXe.Application.Models;

public enum AppThemeMode
{
    Light,
    Dark,
    Auto
}

public sealed class ThemeSettings
{
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Light;
    public int AutoDarkStartHour { get; set; } = 18;
    public int AutoLightStartHour { get; set; } = 6;
}

public static class ThemeSchedule
{
    public static bool IsDarkHour(DateTime localTime, int darkStartHour = 18, int lightStartHour = 6)
    {
        var hour = localTime.Hour;
        return hour >= darkStartHour || hour < lightStartHour;
    }

    public static bool ResolveIsDark(ThemeSettings settings, DateTime? localTime = null)
    {
        localTime ??= DateTime.Now;
        return settings.ThemeMode switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            AppThemeMode.Auto => IsDarkHour(localTime.Value, settings.AutoDarkStartHour, settings.AutoLightStartHour),
            _ => false
        };
    }
}
