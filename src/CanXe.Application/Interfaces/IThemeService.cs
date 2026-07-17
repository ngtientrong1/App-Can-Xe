using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IThemeService
{
    ThemeSettings Settings { get; }

    bool IsDarkActive { get; }

    event EventHandler? ThemeChanged;

    void Load();

    void Save();

    void ApplyThemeMode(AppThemeMode mode);

    void RefreshAutoThemeIfNeeded();
}
