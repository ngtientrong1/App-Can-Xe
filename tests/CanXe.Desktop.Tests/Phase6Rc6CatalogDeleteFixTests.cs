using System.IO;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using CanXe.Desktop.Views.Catalogs;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

public sealed class Phase6Rc6CatalogUiTests
{
    private static string ResolveXaml(string relativeUnderDesktop)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", relativeUnderDesktop);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relativeUnderDesktop}");
    }

    [Fact]
    public void CatalogEditDialog_HasDeleteButton()
    {
        var xaml = File.ReadAllText(ResolveXaml(Path.Combine("Views", "Catalogs", "CatalogEditWindow.xaml")));
        Assert.Contains("x:Name=\"DeleteButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"XÓA\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"Delete_Click\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogView_ActionColumn_HasEditAndDeleteIcons()
    {
        var xaml = File.ReadAllText(ResolveXaml(Path.Combine("Views", "Catalogs", "CatalogView.xaml")));
        Assert.Contains("CanXeCatalogEditButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("CanXeCatalogDeleteButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Hành động\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Địa chỉ\"", xaml, StringComparison.Ordinal);
    }
}

[Collection("WpfSta")]
public sealed class Phase6Rc6CatalogWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase6Rc6CatalogWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task OpenCatalogFirstTime_DoesNotThrow()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        Exception? fault = null;
        await _fixture.InvokeAsync(async _ =>
        {
            try
            {
                vm.NavigateCommand.Execute(AppNavigationSection.Catalog);
                await vm.Catalog.InitializeAsync();
            }
            catch (Exception ex)
            {
                fault = ex;
            }
        });

        Assert.Null(fault);
        Assert.NotNull(vm.Catalog.Rows);
        Assert.False(vm.Catalog.IsCatalogEditEnabled);
    }

    [Fact]
    public async Task CatalogInitialLoad_NullSafe()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var catalog = new CatalogViewModel(
            host.Provider.GetRequiredService<ICatalogService>(),
            host.Provider.GetRequiredService<IPrintNotificationService>(),
            host.Provider.GetRequiredService<IUserPermissionService>(),
            host.Provider.GetRequiredService<IAdminAuthorizationService>());

        await _fixture.InvokeAsync(async _ =>
        {
            await catalog.InitializeAsync();
            catalog.SelectedTabIndex = 1;
            await catalog.RefreshCommand.ExecuteAsync(null);
            catalog.SelectedTabIndex = 2;
            await catalog.RefreshCommand.ExecuteAsync(null);
            catalog.SelectedTabIndex = 0;
            await catalog.RefreshCommand.ExecuteAsync(null);
        });

        Assert.NotNull(catalog.Rows);
        Assert.False(string.IsNullOrWhiteSpace(catalog.StatusMessage));
        Assert.DoesNotContain("DateTimeOffset", catalog.StatusMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("SQLite", catalog.StatusMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerEditDialog_AdminCanDelete()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();
        Assert.True(auth.TryUnlock("admin123").Success);

        await _fixture.InvokeAsync(_ =>
        {
            var row = new CatalogRowItem
            {
                Id = 1,
                Tab = CatalogTab.Customer,
                PrimaryName = "Khách dialog",
                IsActive = true
            };
            var dialog = new CatalogEditWindow(CatalogTab.Customer, row, allowDelete: true);
            Assert.Equal(System.Windows.Visibility.Visible, dialog.FindName("DeleteButton") is System.Windows.Controls.Button b
                ? b.Visibility
                : System.Windows.Visibility.Collapsed);
            dialog.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task VehicleEditDialog_AdminCanDelete()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        await _fixture.InvokeAsync(_ =>
        {
            var row = new CatalogRowItem
            {
                Id = 2,
                Tab = CatalogTab.Vehicle,
                PrimaryName = "51A-1",
                IsActive = true
            };
            var dialog = new CatalogEditWindow(CatalogTab.Vehicle, row, allowDelete: true);
            var button = (System.Windows.Controls.Button)dialog.FindName("DeleteButton")!;
            Assert.Equal(System.Windows.Visibility.Visible, button.Visibility);
            dialog.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CargoEditDialog_AdminCanDelete()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        await _fixture.InvokeAsync(_ =>
        {
            var row = new CatalogRowItem
            {
                Id = 3,
                Tab = CatalogTab.CargoType,
                PrimaryName = "Gạo",
                IsActive = true
            };
            var dialog = new CatalogEditWindow(CatalogTab.CargoType, row, allowDelete: true);
            var button = (System.Windows.Controls.Button)dialog.FindName("DeleteButton")!;
            Assert.Equal(System.Windows.Visibility.Visible, button.Visibility);

            var addDialog = new CatalogEditWindow(CatalogTab.CargoType, null, allowDelete: true);
            var addButton = (System.Windows.Controls.Button)addDialog.FindName("DeleteButton")!;
            Assert.Equal(System.Windows.Visibility.Collapsed, addButton.Visibility);
            dialog.Close();
            addDialog.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task OperatorCannotDelete_CatalogEditDisabled()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();
        var permissions = host.Provider.GetRequiredService<IUserPermissionService>();
        Assert.False(auth.IsAdminUnlocked);

        var catalogVm = new CatalogViewModel(
            host.Provider.GetRequiredService<ICatalogService>(),
            host.Provider.GetRequiredService<IPrintNotificationService>(),
            permissions,
            auth);
        catalogVm.NotifyAdminPermissionsChanged();

        Assert.False(catalogVm.IsCatalogEditEnabled);
        Assert.False(permissions.HasPermission(AdminPermission.CanDeleteCatalogItem));
    }
}
