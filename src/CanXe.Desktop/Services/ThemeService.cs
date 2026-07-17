using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Infrastructure.Logging;

namespace CanXe.Desktop.Services;

public sealed class ThemeService : IThemeService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsPath;
    private readonly DispatcherTimer _autoTimer;
    private readonly EventHandler _autoTimerHandler;
    private bool _isDarkActive;
    private bool _disposed;

    public ThemeService()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "theme-settings.json");

        _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _autoTimerHandler = (_, _) => RefreshAutoThemeIfNeeded();
        _autoTimer.Tick += _autoTimerHandler;
    }

    public ThemeSettings Settings { get; private set; } = new();

    public bool IsDarkActive => _isDarkActive;

    public event EventHandler? ThemeChanged;

    public void Load()
    {
        Settings = ReadSettings();
        ApplyResolvedTheme(ThemeSchedule.ResolveIsDark(Settings));
        _autoTimer.Start();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            var tmp = _settingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _settingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppExceptionLogger.WriteError("ThemeService.Save", ex);
        }
    }

    public void ApplyThemeMode(AppThemeMode mode)
    {
        Settings.ThemeMode = mode;
        Save();
        ApplyResolvedTheme(ThemeSchedule.ResolveIsDark(Settings));
    }

    public void RefreshAutoThemeIfNeeded()
    {
        if (Settings.ThemeMode != AppThemeMode.Auto)
            return;

        var shouldBeDark = ThemeSchedule.ResolveIsDark(Settings);
        if (shouldBeDark == _isDarkActive)
            return;

        ApplyResolvedTheme(shouldBeDark);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _autoTimer.Stop();
        _autoTimer.Tick -= _autoTimerHandler;
    }

    private ThemeSettings ReadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new ThemeSettings();

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<ThemeSettings>(json, JsonOptions) ?? new ThemeSettings();
        }
        catch (Exception ex)
        {
            AppExceptionLogger.WriteError("ThemeService.Load", ex);
            return new ThemeSettings();
        }
    }

    private void ApplyResolvedTheme(bool dark)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        var merged = app.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(IsThemeDictionary);
        if (existing is not null)
            merged.Remove(existing);

        var themeFile = dark ? "DarkTheme.xaml" : "LightTheme.xaml";
        var uri = new Uri($"/CanXe.Desktop;component/Themes/{themeFile}", UriKind.Relative);
        merged.Insert(0, new ResourceDictionary { Source = uri });

        _isDarkActive = dark;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        return source is not null &&
               (source.Contains("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase));
    }
}
