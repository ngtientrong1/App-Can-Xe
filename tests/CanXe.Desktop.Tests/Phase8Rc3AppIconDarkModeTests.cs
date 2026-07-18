using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanXe.Application.Models;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

public sealed class Phase8Rc3AppIconDarkModeTests
{
    private static string ResolveRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private static string ResolveDesktopFile(string relative) =>
        ResolveRepoFile(Path.Combine("src", "CanXe.Desktop", relative));

    [Fact]
    public void AppIcon_ApplicationIconConfigured()
    {
        var csproj = File.ReadAllText(ResolveDesktopFile("CanXe.Desktop.csproj"));
        Assert.Contains("ApplicationIcon>Assets\\canxe-tien-trong.ico", csproj, StringComparison.Ordinal);
        Assert.Contains("Assets\\canxe-tien-trong.ico", csproj, StringComparison.Ordinal);
        Assert.True(File.Exists(ResolveDesktopFile(Path.Combine("Assets", "canxe-tien-trong.ico"))));
    }

    [Fact]
    public void AppIcon_MainWindowIconConfigured()
    {
        var xaml = File.ReadAllText(ResolveDesktopFile("MainWindow.xaml"));
        Assert.Contains("Icon=\"pack://application:,,,/CanXe.Desktop;component/Assets/canxe-tien-trong.ico\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Title=\"Cân Xe Tiến Trọng\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_UsesSetupIcon()
    {
        var iss = File.ReadAllText(ResolveRepoFile(Path.Combine("installer", "CanXeTienTrong.iss")));
        Assert.Contains("SetupIconFile=", iss, StringComparison.Ordinal);
        Assert.Contains("canxe-tien-trong.ico", iss, StringComparison.Ordinal);
        Assert.Contains("UninstallDisplayIcon={app}\\{#MyAppExeName}", iss, StringComparison.Ordinal);
        Assert.Contains("IconFilename: \"{app}\\{#MyAppExeName}\"", iss, StringComparison.Ordinal);
        Assert.Contains("#define MyAppVersion \"1.0.4\"", iss, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkTheme_CatalogTabItemsReadable()
    {
        var design = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "CanXeDesignSystem.xaml")));
        Assert.Contains("TargetType=\"TabItem\"", design, StringComparison.Ordinal);
        Assert.Contains("TabItemBackgroundBrush", design, StringComparison.Ordinal);
        Assert.Contains("TabItemSelectedBackgroundBrush", design, StringComparison.Ordinal);
        Assert.Contains("ControlTemplate TargetType=\"TabItem\"", design, StringComparison.Ordinal);

        var dark = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "DarkTheme.xaml")));
        Assert.Contains("x:Key=\"TabItemBackgroundBrush\"", dark, StringComparison.Ordinal);
        Assert.DoesNotContain("TabItemBackgroundBrush\" Color=\"#FFFFFF\"", dark, StringComparison.Ordinal);
        Assert.DoesNotContain("TabItemBackgroundBrush\" Color=\"#FFF\"", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkTheme_ReportDatePickerReadable()
    {
        var design = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "CanXeDesignSystem.xaml")));
        Assert.Contains("TargetType=\"DatePicker\"", design, StringComparison.Ordinal);
        Assert.Contains("DatePickerBackgroundBrush", design, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type DatePickerTextBox}\"", design, StringComparison.Ordinal);

        var dark = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "DarkTheme.xaml")));
        Assert.Contains("x:Key=\"DatePickerBackgroundBrush\"", dark, StringComparison.Ordinal);
        Assert.DoesNotContain("DatePickerBackgroundBrush\" Color=\"#FFFFFF\"", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkTheme_ReportComboBoxReadable()
    {
        var design = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "CanXeDesignSystem.xaml")));
        Assert.Contains("CanXeComboBoxStyle", design, StringComparison.Ordinal);
        Assert.Contains("ComboBoxDropdownBackgroundBrush", design, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"ComboBoxItem\"", design, StringComparison.Ordinal);

        var dark = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "DarkTheme.xaml")));
        Assert.Contains("x:Key=\"ComboBoxDropdownBackgroundBrush\"", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkTheme_ReportFilterTextBoxesReadable()
    {
        var design = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "CanXeDesignSystem.xaml")));
        Assert.Contains("CanXeTextBoxStyle", design, StringComparison.Ordinal);
        Assert.Contains("InputBackgroundBrush", design, StringComparison.Ordinal);
        Assert.Contains("InputForegroundBrush", design, StringComparison.Ordinal);

        var report = File.ReadAllText(ResolveDesktopFile(Path.Combine("Views", "Reports", "ReportView.xaml")));
        Assert.Contains("Style=\"{StaticResource CanXeTextBoxStyle}\"", report, StringComparison.Ordinal);
    }

    [Fact]
    public void LightTheme_StillLoads()
    {
        var light = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "LightTheme.xaml")));
        Assert.Contains("x:Key=\"TabItemBackgroundBrush\"", light, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"DatePickerBackgroundBrush\"", light, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ComboBoxDropdownBackgroundBrush\"", light, StringComparison.Ordinal);
        Assert.Contains("Color=\"#FFFFFF\"", light, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_DoesNotAffectPrintTemplate()
    {
        var code = File.ReadAllText(ResolveDesktopFile(Path.Combine("Services", "WpfWeighTicketDocumentFactory.cs")));
        Assert.DoesNotContain("ThemeService", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DarkTheme", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_DoesNotAffectExcelExport()
    {
        var code = File.ReadAllText(ResolveRepoFile(Path.Combine("src", "CanXe.Infrastructure", "Services", "ExcelReportExporter.cs")));
        Assert.DoesNotContain("ThemeService", code, StringComparison.Ordinal);
        Assert.DoesNotContain("AppThemeMode", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Desktop_Version_Is_1_0_4()
    {
        var csproj = File.ReadAllText(ResolveDesktopFile("CanXe.Desktop.csproj"));
        Assert.Contains("<Version>1.0.4</Version>", csproj, StringComparison.Ordinal);
        Assert.Contains("<InformationalVersion>1.0.4</InformationalVersion>", csproj, StringComparison.Ordinal);
    }
}

[Collection("WpfSta")]
public sealed class Phase8Rc3ThemeControlWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase8Rc3ThemeControlWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LightTheme_StillLoads()
    {
        await _fixture.InvokeAsync(_ =>
        {
            var service = new ThemeService();
            service.Load();
            service.ApplyThemeMode(AppThemeMode.Light);
            Assert.False(service.IsDarkActive);
            Assert.NotNull(System.Windows.Application.Current.TryFindResource("TabItemBackgroundBrush"));
            Assert.NotNull(System.Windows.Application.Current.TryFindResource("DatePickerBackgroundBrush"));
            Assert.NotNull(System.Windows.Application.Current.TryFindResource("InputBackgroundBrush"));
            service.Dispose();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task DarkTheme_CatalogAndReportControlsUseThemedBrushes()
    {
        await _fixture.InvokeAsync(_ =>
        {
            var service = new ThemeService();
            service.Load();
            service.ApplyThemeMode(AppThemeMode.Dark);
            Assert.True(service.IsDarkActive);

            var tabBg = (SolidColorBrush)System.Windows.Application.Current.FindResource("TabItemBackgroundBrush");
            var dateBg = (SolidColorBrush)System.Windows.Application.Current.FindResource("DatePickerBackgroundBrush");
            var comboBg = (SolidColorBrush)System.Windows.Application.Current.FindResource("ComboBoxDropdownBackgroundBrush");
            var inputBg = (SolidColorBrush)System.Windows.Application.Current.FindResource("InputBackgroundBrush");

            Assert.NotEqual(Colors.White, tabBg.Color);
            Assert.NotEqual(Colors.White, dateBg.Color);
            Assert.NotEqual(Colors.White, comboBg.Color);
            Assert.NotEqual(Colors.White, inputBg.Color);

            Assert.NotNull(System.Windows.Application.Current.TryFindResource(typeof(DatePicker)));
            Assert.NotNull(System.Windows.Application.Current.TryFindResource(typeof(ComboBox)));
            Assert.NotNull(System.Windows.Application.Current.TryFindResource(typeof(TabItem)));

            service.Dispose();
            return Task.CompletedTask;
        });
    }
}
