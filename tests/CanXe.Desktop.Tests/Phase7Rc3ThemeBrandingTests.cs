using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using CanXe.Application.Models;
using CanXe.Desktop;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

public sealed class Phase7Rc3ThemeScheduleTests
{
    [Fact]
    public void Theme_DefaultMode_IsLightOrConfiguredDefault()
    {
        var settings = new ThemeSettings();
        Assert.Equal(AppThemeMode.Light, settings.ThemeMode);
        Assert.Equal(18, settings.AutoDarkStartHour);
        Assert.Equal(6, settings.AutoLightStartHour);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(18, true)]
    [InlineData(23, true)]
    [InlineData(6, false)]
    [InlineData(12, false)]
    [InlineData(17, false)]
    public void Theme_AutoUsesDarkOrLightByHour(int hour, bool expectDark)
    {
        var time = new DateTime(2026, 7, 16, hour, 30, 0);
        var settings = new ThemeSettings { ThemeMode = AppThemeMode.Auto };
        Assert.Equal(expectDark, ThemeSchedule.ResolveIsDark(settings, time));
    }

    [Fact]
    public void Theme_AutoUsesDarkAfter18() =>
        Assert.True(ThemeSchedule.ResolveIsDark(new ThemeSettings { ThemeMode = AppThemeMode.Auto },
            new DateTime(2026, 7, 16, 18, 0, 0)));

    [Fact]
    public void Theme_AutoUsesLightAfter6() =>
        Assert.False(ThemeSchedule.ResolveIsDark(new ThemeSettings { ThemeMode = AppThemeMode.Auto },
            new DateTime(2026, 7, 16, 6, 0, 0)));

    [Fact]
    public void Theme_CanSwitchToLight() =>
        Assert.False(ThemeSchedule.ResolveIsDark(new ThemeSettings { ThemeMode = AppThemeMode.Light }));

    [Fact]
    public void Theme_CanSwitchToDark() =>
        Assert.True(ThemeSchedule.ResolveIsDark(new ThemeSettings { ThemeMode = AppThemeMode.Dark }));
}

public sealed class Phase7Rc3ThemeBrandingUiTests
{
    private static string MainWindowXaml => ResolveDesktopFile("MainWindow.xaml");
    private static string LightThemeXaml => ResolveDesktopFile(Path.Combine("Themes", "LightTheme.xaml"));
    private static string DarkThemeXaml => ResolveDesktopFile(Path.Combine("Themes", "DarkTheme.xaml"));

    private static string ResolveDesktopFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relative}");
    }

    [Fact]
    public void Branding_HeaderShowsCanXeTienTrong()
    {
        var xaml = File.ReadAllText(MainWindowXaml);
        Assert.Contains("Cân Xe Tiến Trọng", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"CanXe\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Branding_HeaderDoesNotShowComPill()
    {
        var xaml = File.ReadAllText(MainWindowXaml);
        Assert.DoesNotContain("HeaderScaleStatus", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CanXeStatusBadgeStyle", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowTitle_IsCanXeTienTrong()
    {
        var xaml = File.ReadAllText(MainWindowXaml);
        Assert.Contains("Title=\"Cân Xe Tiến Trọng\"", xaml, StringComparison.Ordinal);
        Assert.Equal(AppBranding.WindowTitle, AppBranding.DisplayName);
    }

    [Fact]
    public void ThemeSetting_HasAppearanceSectionInSettingsTab()
    {
        var xaml = File.ReadAllText(MainWindowXaml);
        Assert.Contains("Text=\"GIAO DIỆN\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Sáng\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Tối\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Tự động\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectThemeLightCommand", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("LightTheme.xaml")]
    [InlineData("DarkTheme.xaml")]
    public void Theme_HasRequiredBrushResources(string themeFile)
    {
        var path = themeFile.Contains("Dark", StringComparison.Ordinal) ? DarkThemeXaml : LightThemeXaml;
        var xaml = File.ReadAllText(path);
        foreach (var key in RequiredBrushKeys)
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void LightTheme_HasRequiredBrushResources() =>
        Theme_HasRequiredBrushResources("LightTheme.xaml");

    [Fact]
    public void DarkTheme_HasRequiredBrushResources() =>
        Theme_HasRequiredBrushResources("DarkTheme.xaml");

    [Fact]
    public void Theme_DoesNotAffectPrintTemplate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? printFile = null;
        while (dir is not null)
        {
            var factory = Path.Combine(dir.FullName, "src", "CanXe.Desktop", "Services", "WpfWeighTicketDocumentFactory.cs");
            if (File.Exists(factory))
            {
                printFile = factory;
                break;
            }

            dir = dir.Parent;
        }

        Assert.NotNull(printFile);
        var code = File.ReadAllText(printFile!);
        Assert.DoesNotContain("ThemeService", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DarkTheme", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DynamicResource", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_DoesNotAffectExcelExport()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? exporter = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Infrastructure", "Services", "ExcelReportExporter.cs");
            if (File.Exists(candidate))
            {
                exporter = candidate;
                break;
            }

            dir = dir.Parent;
        }

        Assert.NotNull(exporter);
        var code = File.ReadAllText(exporter!);
        Assert.DoesNotContain("ThemeService", code, StringComparison.Ordinal);
        Assert.DoesNotContain("AppThemeMode", code, StringComparison.Ordinal);
    }

    private static readonly string[] RequiredBrushKeys =
    [
        "AppBackgroundBrush",
        "CardBackgroundBrush",
        "PanelBackgroundBrush",
        "PrimaryTextBrush",
        "SecondaryTextBrush",
        "MutedTextBrush",
        "AccentTextBrush",
        "SuccessTextBrush",
        "WarningTextBrush",
        "DangerTextBrush",
        "BorderBrush",
        "AccentBrush",
        "AccentHoverBrush",
        "SuccessGreenBrush",
        "WarningBrush",
        "DangerBrush",
        "InputBackgroundBrush",
        "InputBorderBrush",
        "DataGridHeaderBackgroundBrush",
        "DataGridRowBackgroundBrush",
        "DataGridAlternateRowBackgroundBrush",
        "ToastBackgroundBrush",
        "ButtonSecondaryBackgroundBrush",
        "DisabledBackgroundBrush",
        "DisabledTextBrush",
        "TabItemBackgroundBrush",
        "TabItemSelectedBackgroundBrush",
        "TabItemHoverBackgroundBrush",
        "ComboBoxDropdownBackgroundBrush",
        "DatePickerBackgroundBrush"
    ];
}

[Collection("WpfSta")]
public sealed class Phase7Rc3ThemeServiceWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase7Rc3ThemeServiceWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Theme_CanSwitchToAuto()
    {
        await _fixture.InvokeAsync(_ =>
        {
            var service = new ThemeService();
            service.Load();
            service.ApplyThemeMode(AppThemeMode.Auto);
            Assert.Equal(AppThemeMode.Auto, service.Settings.ThemeMode);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Theme_SettingPersistsAfterRestart()
    {
        await _fixture.InvokeAsync(async _ =>
        {
            var first = new ThemeService();
            first.Load();
            first.ApplyThemeMode(AppThemeMode.Dark);
            var settingsPath = typeof(ThemeService)
                .GetField("_settingsPath", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(first) as string;
            first.Dispose();

            Assert.False(string.IsNullOrWhiteSpace(settingsPath));
            Assert.True(File.Exists(settingsPath!));

            var second = new ThemeService();
            second.Load();
            Assert.Equal(AppThemeMode.Dark, second.Settings.ThemeMode);
            Assert.True(second.IsDarkActive);
            second.Dispose();

            var json = await File.ReadAllTextAsync(settingsPath!);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("Dark", doc.RootElement.GetProperty("themeMode").GetString());
            return;
        });
    }
}

public sealed class Phase7Rc3ThemeBackupTests
{
    [Fact]
    public void BackupService_IncludesThemeSettingsFileWhenPresent()
    {
        var code = File.ReadAllText(ResolveBackupService());
        Assert.Contains("theme-settings.json", code, StringComparison.Ordinal);
    }

    private static string ResolveBackupService()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Infrastructure", "Services", "BackupService.cs");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("BackupService.cs not found");
    }
}
