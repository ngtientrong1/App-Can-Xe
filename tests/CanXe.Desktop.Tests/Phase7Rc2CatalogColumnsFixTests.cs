using System.IO;
using System.Windows;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

public sealed class Phase7Rc2CatalogColumnsUiTests
{
    private static string CatalogViewXamlPath => ResolvePath("Views", "Catalogs", "CatalogView.xaml");

    private static string ResolvePath(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "src", "CanXe.Desktop" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate CatalogView.xaml.");
    }

    private static string CustomerGridSection(string xaml) =>
        ExtractBetween(xaml, "x:Name=\"CustomerCatalogGrid\"", "x:Name=\"VehicleCatalogGrid\"");

    private static string VehicleGridSection(string xaml) =>
        ExtractBetween(xaml, "x:Name=\"VehicleCatalogGrid\"", "x:Name=\"CargoCatalogGrid\"");

    private static string CargoGridSection(string xaml) =>
        ExtractBetween(xaml, "x:Name=\"CargoCatalogGrid\"", "</Grid>");

    private static string ExtractBetween(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Missing marker: {start}");
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"Missing end marker: {end}");
        return text[from..to];
    }

    [Fact]
    public void CustomerCatalog_ShowsOnlyCustomerColumns()
    {
        var xaml = File.ReadAllText(CatalogViewXamlPath);
        var section = CustomerGridSection(xaml);
        Assert.Contains("Header=\"Tên khách hàng\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"SĐT\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Ghi chú\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Số phiếu\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Dùng gần nhất\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Hành động\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomerCatalog_DoesNotShowVehicleOrCargoColumns()
    {
        var section = CustomerGridSection(File.ReadAllText(CatalogViewXamlPath));
        Assert.DoesNotContain("Header=\"Biển số\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Loại hàng\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Chủ xe / khách hàng\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Đơn giá mặc định\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Đơn vị\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Địa chỉ\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void VehicleCatalog_ShowsOnlyVehicleColumns()
    {
        var section = VehicleGridSection(File.ReadAllText(CatalogViewXamlPath));
        Assert.Contains("Header=\"Biển số\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Chủ xe / khách hàng\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Ghi chú\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Số phiếu\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Dùng gần nhất\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Hành động\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void VehicleCatalog_DoesNotShowCustomerOrCargoColumns()
    {
        var section = VehicleGridSection(File.ReadAllText(CatalogViewXamlPath));
        Assert.DoesNotContain("Header=\"Tên khách hàng\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Loại hàng\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"SĐT\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Đơn giá mặc định\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Đơn vị\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void CargoCatalog_ShowsOnlyCargoColumns()
    {
        var section = CargoGridSection(File.ReadAllText(CatalogViewXamlPath));
        Assert.Contains("Header=\"Loại hàng\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Đơn giá mặc định\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Đơn vị\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Ghi chú\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Số phiếu\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Dùng gần nhất\"", section, StringComparison.Ordinal);
        Assert.Contains("Header=\"Hành động\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void CargoCatalog_DoesNotShowCustomerOrVehicleColumns()
    {
        var section = CargoGridSection(File.ReadAllText(CatalogViewXamlPath));
        Assert.DoesNotContain("Header=\"Tên khách hàng\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Biển số\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Chủ xe / khách hàng\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"SĐT\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomerCatalog_DoesNotDuplicateNameIntoPlateCargo()
    {
        var section = CustomerGridSection(File.ReadAllText(CatalogViewXamlPath));
        var primaryNameCount = CountOccurrences(section, "Binding=\"{Binding PrimaryName}\"");
        Assert.Equal(1, primaryNameCount);
    }

    [Fact]
    public void VehicleCatalog_DoesNotDuplicatePlateIntoCustomerCargo()
    {
        var section = VehicleGridSection(File.ReadAllText(CatalogViewXamlPath));
        var primaryNameCount = CountOccurrences(section, "Binding=\"{Binding PrimaryName}\"");
        Assert.Equal(1, primaryNameCount);
    }

    [Fact]
    public void CargoCatalog_DoesNotDuplicateCargoIntoCustomerPlate()
    {
        var section = CargoGridSection(File.ReadAllText(CatalogViewXamlPath));
        var primaryNameCount = CountOccurrences(section, "Binding=\"{Binding PrimaryName}\"");
        Assert.Equal(1, primaryNameCount);
    }

    [Fact]
    public void CatalogView_UsesThreeTabSpecificGrids()
    {
        var xaml = File.ReadAllText(CatalogViewXamlPath);
        Assert.Contains("x:Name=\"CustomerCatalogGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"VehicleCatalogGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CargoCatalogGrid\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CatalogTabColumnVisibilityConverter", xaml, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}

[Collection("WpfSta")]
public sealed class Phase7Rc2CatalogRegressionWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase7Rc2CatalogRegressionWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CatalogDelete_StillWorksAfterColumnFix()
    {
        await using var host = new DesktopTestHost(new AppSettings { DeveloperMode = true, ShowDeveloperTab = true });
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();
        Assert.True(auth.TryUnlock("admin123").Success);

        var catalog = new CatalogViewModel(
            host.Provider.GetRequiredService<ICatalogService>(),
            host.Provider.GetRequiredService<IPrintNotificationService>(),
            host.Provider.GetRequiredService<IUserPermissionService>(),
            auth);
        catalog.NotifyAdminPermissionsChanged();

        Assert.True(catalog.IsCatalogEditEnabled);
        Assert.True(host.Provider.GetRequiredService<IUserPermissionService>()
            .HasPermission(AdminPermission.CanDeleteCatalogItem));
    }

    [Fact]
    public async Task CatalogEdit_StillWorksAfterColumnFix()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();
        Assert.True(auth.TryUnlock("admin123").Success);

        var catalog = host.Provider.GetRequiredService<CatalogViewModel>();
        catalog.NotifyAdminPermissionsChanged();

        await _fixture.InvokeAsync(async _ =>
        {
            await catalog.InitializeAsync();
            Assert.True(host.Provider.GetRequiredService<IUserPermissionService>()
                .HasPermission(AdminPermission.CanEditCatalogItem));
        });
    }

    [Fact]
    public async Task CustomerTab_ShowsOnlyCustomerColumnHeaders()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        await _fixture.InvokeAsync(async _ =>
        {
            vm.ActiveSection = AppNavigationSection.Catalog;
            await vm.Catalog.InitializeAsync();

            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = vm,
                Width = 1366,
                Height = 768
            };
            window.Show();
            window.UpdateLayout();

            vm.Catalog.SelectedTabIndex = 0;
            window.UpdateLayout();

            var grid = ShellLayoutTestHelpers.FindNamedDataGrid(window, "CustomerCatalogGrid");
            Assert.NotNull(grid);
            var headers = ShellLayoutTestHelpers.VisibleDataGridColumnHeaders(grid);
            Assert.Contains("Tên khách hàng", headers);
            Assert.Contains("SĐT", headers);
            Assert.Contains("Ghi chú", headers);
            Assert.DoesNotContain("Biển số", headers);
            Assert.DoesNotContain("Loại hàng", headers);
            Assert.DoesNotContain("Chủ xe / khách hàng", headers);
            Assert.DoesNotContain("Đơn giá mặc định", headers);

            window.Close();
        });
    }

    [Fact]
    public async Task VehicleTab_ShowsOnlyVehicleColumnHeaders()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        await _fixture.InvokeAsync(async _ =>
        {
            vm.ActiveSection = AppNavigationSection.Catalog;
            await vm.Catalog.InitializeAsync();

            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = vm,
                Width = 1366,
                Height = 768
            };
            window.Show();
            window.UpdateLayout();

            vm.Catalog.SelectedTabIndex = 1;
            window.UpdateLayout();

            var grid = ShellLayoutTestHelpers.FindNamedDataGrid(window, "VehicleCatalogGrid");
            Assert.NotNull(grid);
            var headers = ShellLayoutTestHelpers.VisibleDataGridColumnHeaders(grid);
            Assert.Contains("Biển số", headers);
            Assert.Contains("Chủ xe / khách hàng", headers);
            Assert.DoesNotContain("Tên khách hàng", headers);
            Assert.DoesNotContain("Loại hàng", headers);
            Assert.DoesNotContain("SĐT", headers);

            window.Close();
        });
    }

    [Fact]
    public async Task CargoTab_ShowsOnlyCargoColumnHeaders()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        await _fixture.InvokeAsync(async _ =>
        {
            vm.ActiveSection = AppNavigationSection.Catalog;
            await vm.Catalog.InitializeAsync();

            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = vm,
                Width = 1366,
                Height = 768
            };
            window.Show();
            window.UpdateLayout();

            vm.Catalog.SelectedTabIndex = 2;
            window.UpdateLayout();

            var grid = ShellLayoutTestHelpers.FindNamedDataGrid(window, "CargoCatalogGrid");
            Assert.NotNull(grid);
            var headers = ShellLayoutTestHelpers.VisibleDataGridColumnHeaders(grid);
            Assert.Contains("Loại hàng", headers);
            Assert.Contains("Đơn giá mặc định", headers);
            Assert.Contains("Đơn vị", headers);
            Assert.DoesNotContain("Tên khách hàng", headers);
            Assert.DoesNotContain("Biển số", headers);
            Assert.DoesNotContain("SĐT", headers);

            window.Close();
        });
    }
}
