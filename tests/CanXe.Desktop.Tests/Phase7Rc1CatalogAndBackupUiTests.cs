using System.IO;
using CanXe.Application.Configuration;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

public sealed class Phase7Rc1CatalogFirstOpenUiTests
{
    private static string DesignSystemPath
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", "Themes", "CanXeDesignSystem.xaml");
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException("CanXeDesignSystem.xaml not found.");
        }
    }

    [Fact]
    public void CatalogDeleteStyle_IsDefinedAfterHistoryBaseStyle()
    {
        var xaml = File.ReadAllText(DesignSystemPath);
        var history = xaml.IndexOf("x:Key=\"CanXeHistoryDeleteButtonStyle\"", StringComparison.Ordinal);
        var catalog = xaml.IndexOf("x:Key=\"CanXeCatalogDeleteButtonStyle\"", StringComparison.Ordinal);
        Assert.True(history >= 0);
        Assert.True(catalog >= 0);
        Assert.True(history < catalog, "History delete style must be declared before Catalog delete BasedOn.");
    }

    [Fact]
    public void SystemTab_HasBackupSection_NotLegacyButton()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? mainWindow = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", "MainWindow.xaml");
            if (File.Exists(candidate))
            {
                mainWindow = candidate;
                break;
            }

            dir = dir.Parent;
        }

        Assert.NotNull(mainWindow);
        var xaml = File.ReadAllText(mainWindow!);
        Assert.Contains("SAO LƯU &amp; KHÔI PHỤC DỮ LIỆU", xaml, StringComparison.Ordinal);
        Assert.Contains("TẠO BACKUP NGAY", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SAO LƯU DATABASE", xaml, StringComparison.Ordinal);
        Assert.Contains("CreateBackupNowCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("RestoreFromBackupCommand", xaml, StringComparison.Ordinal);
    }
}

[Collection("WpfSta")]
public sealed class Phase7Rc1CatalogFirstOpenWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase7Rc1CatalogFirstOpenWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task OpenCatalogFirstTime_DoesNotShowGlobalError()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        Exception? fault = null;
        await _fixture.InvokeAsync(async _ =>
        {
            try
            {
                vm.NavigateCommand.Execute(Application.Models.AppNavigationSection.Catalog);
                await vm.Catalog.InitializeAsync();
            }
            catch (Exception ex)
            {
                fault = ex;
            }
        });

        Assert.Null(fault);
        Assert.DoesNotContain("CanXeHistoryDeleteButtonStyle", vm.Catalog.StatusMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("XamlParse", vm.Catalog.StatusMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CatalogLoadFailure_DoesNotBubbleToDispatcher()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var catalog = host.Provider.GetRequiredService<CatalogViewModel>();
        Exception? fault = null;
        await _fixture.InvokeAsync(async _ =>
        {
            try
            {
                await catalog.InitializeAsync();
            }
            catch (Exception ex)
            {
                fault = ex;
            }
        });
        Assert.Null(fault);
    }
}
