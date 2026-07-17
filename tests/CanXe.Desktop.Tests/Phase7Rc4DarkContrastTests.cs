using System.IO;
using System.Windows;
using System.Windows.Media;
using CanXe.Application.Models;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

public sealed class Phase7Rc4DarkContrastTests
{
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

    private static string MainWindowXaml => ResolveDesktopFile("MainWindow.xaml");
    private static string DarkThemeXaml => ResolveDesktopFile(Path.Combine("Themes", "DarkTheme.xaml"));
    private static string LightThemeXaml => ResolveDesktopFile(Path.Combine("Themes", "LightTheme.xaml"));
    private static string DesignSystemXaml => ResolveDesktopFile(Path.Combine("Themes", "CanXeDesignSystem.xaml"));

    [Fact]
    public void DarkTheme_HeaderAppName_UsesReadableBrush()
    {
        var xaml = File.ReadAllText(MainWindowXaml);
        Assert.Contains("Text=\"Cân Xe Tiến Trọng\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource PrimaryTextBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Title=\"Cân Xe Tiến Trọng\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource PrimaryTextBrush}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkTheme_HeaderDateTime_UsesReadableBrush()
    {
        var xaml = File.ReadAllText(MainWindowXaml);
        Assert.Contains("HeaderClockText", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource SecondaryTextBrush}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkTheme_RequiredBrushesExist()
    {
        var xaml = File.ReadAllText(DarkThemeXaml);
        foreach (var key in new[]
                 {
                     "PrimaryTextBrush", "SecondaryTextBrush", "MutedTextBrush",
                     "AccentTextBrush", "SuccessTextBrush", "WarningTextBrush", "DangerTextBrush",
                     "DisabledTextBrush", "InputForegroundBrush", "DataGridHeaderBackgroundBrush"
                 })
        {
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Color=\"#000000\"", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Color=\"#111111\"", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DarkTheme_DisabledButtonTextNotBlack()
    {
        var design = File.ReadAllText(DesignSystemXaml);
        Assert.Contains("DisabledTextBrush", design, StringComparison.Ordinal);
        Assert.Contains("IsEnabled\" Value=\"False\"", design, StringComparison.Ordinal);
        Assert.Contains("Foreground\" Value=\"{DynamicResource DisabledTextBrush}\"", design, StringComparison.Ordinal);

        var dark = File.ReadAllText(DarkThemeXaml);
        Assert.Contains("x:Key=\"DisabledTextBrush\"", dark, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Key=\"DisabledTextBrush\" Color=\"#000", dark, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DarkTheme_DataGridTextNotBlack()
    {
        var design = File.ReadAllText(DesignSystemXaml);
        Assert.Contains("CanXeDataGridCellStyle", design, StringComparison.Ordinal);
        Assert.Contains("CanXeDataGridRowStyle", design, StringComparison.Ordinal);

        var main = File.ReadAllText(MainWindowXaml);
        Assert.Contains("RightAlignedCell", main, StringComparison.Ordinal);
        Assert.Contains("GridCellText", main, StringComparison.Ordinal);
        Assert.Contains("Foreground\" Value=\"{DynamicResource PrimaryTextBrush}\"", main, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkTheme_InputTextReadable()
    {
        var design = File.ReadAllText(DesignSystemXaml);
        Assert.Contains("InputForegroundBrush", design, StringComparison.Ordinal);
        Assert.Contains("CanXeTextBoxStyle", design, StringComparison.Ordinal);

        var dark = File.ReadAllText(DarkThemeXaml);
        Assert.Contains("x:Key=\"InputForegroundBrush\"", dark, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"InputBackgroundBrush\"", dark, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"InputBorderBrush\"", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void LightTheme_StillLoads()
    {
        var xaml = File.ReadAllText(LightThemeXaml);
        Assert.Contains("x:Key=\"PrimaryTextBrush\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Color=\"#1A1D21\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"AccentTextBrush\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoTheme_StillWorks()
    {
        Assert.True(ThemeSchedule.ResolveIsDark(new ThemeSettings { ThemeMode = AppThemeMode.Auto },
            new DateTime(2026, 7, 17, 18, 0, 0)));
        Assert.False(ThemeSchedule.ResolveIsDark(new ThemeSettings { ThemeMode = AppThemeMode.Auto },
            new DateTime(2026, 7, 17, 6, 0, 0)));
    }

    [Fact]
    public void Theme_DoesNotAffectPrintTemplate()
    {
        var printView = ResolveDesktopFile(Path.Combine("Views", "Printing", "WeighTicketCopyView.xaml"));
        var xaml = File.ReadAllText(printView);
        Assert.Contains("Foreground=\"Black\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DynamicResource PrimaryTextBrush", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_DoesNotAffectExcelExport()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Infrastructure", "Services", "ExcelReportExporter.cs");
            if (File.Exists(candidate))
            {
                var code = File.ReadAllText(candidate);
                Assert.DoesNotContain("ThemeService", code, StringComparison.Ordinal);
                Assert.DoesNotContain("AppThemeMode", code, StringComparison.Ordinal);
                return;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("ExcelReportExporter.cs not found");
    }

    [Fact]
    public void ReportView_UsesDynamicSecondaryTextBrush()
    {
        var report = File.ReadAllText(ResolveDesktopFile(Path.Combine("Views", "Reports", "ReportView.xaml")));
        Assert.Contains("DynamicResource SecondaryTextBrush", report, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticResource SecondaryTextBrush", report, StringComparison.Ordinal);
    }
}

[Collection("WpfSta")]
public sealed class Phase7Rc4DarkThemeWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase7Rc4DarkThemeWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DarkTheme_PrimaryTextBrush_IsLightEnough()
    {
        await _fixture.InvokeAsync(_ =>
        {
            var service = new ThemeService();
            service.Load();
            service.ApplyThemeMode(AppThemeMode.Dark);

            var brush = System.Windows.Application.Current.TryFindResource("PrimaryTextBrush") as SolidColorBrush;
            Assert.NotNull(brush);
            Assert.True(brush!.Color.R > 200 && brush.Color.G > 200 && brush.Color.B > 200,
                $"PrimaryTextBrush too dark for dark mode: {brush.Color}");

            var disabled = System.Windows.Application.Current.TryFindResource("DisabledTextBrush") as SolidColorBrush;
            Assert.NotNull(disabled);
            Assert.True(disabled!.Color.R > 140,
                $"DisabledTextBrush too dark: {disabled.Color}");

            service.ApplyThemeMode(AppThemeMode.Light);
            service.Dispose();
            return Task.CompletedTask;
        });
    }
}
