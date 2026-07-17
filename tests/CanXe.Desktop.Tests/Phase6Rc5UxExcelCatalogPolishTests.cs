using System.IO;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

public sealed class Phase6Rc5CatalogUiTests
{
    private static string CatalogViewXamlPath => ResolveCatalogViewXamlPath();

    private static string ResolveCatalogViewXamlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", "Views", "Catalogs", "CatalogView.xaml");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate CatalogView.xaml.");
    }

    [Fact]
    public void CustomerCatalog_HidesAddressColumn()
    {
        var xaml = File.ReadAllText(CatalogViewXamlPath);
        Assert.DoesNotContain("Header=\"Địa chỉ\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerAddress", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Tên khách hàng\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"SĐT\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Ghi chú\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Số phiếu\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Dùng gần nhất\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Hành động\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CanXeCatalogDeleteButtonStyle", xaml, StringComparison.Ordinal);
    }
}

[Collection("WpfSta")]
public sealed class Phase6Rc5UxPolishWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase6Rc5UxPolishWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AutoFill_DoesNotShowLargeToast()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách autofill rc5";
            vm.LicensePlate = "51AF-RC5";
            vm.CargoTypeName = "Gạo rc5";
            scale.SetManualWeightKg(18000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(9000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            Assert.Null(vm.CustomerName);
            Assert.Null(vm.LicensePlate);

            await vm.OnVehiclePlateCommittedAsync("51AF-RC5");
        });

        Assert.Equal("Khách autofill rc5", vm.CustomerName);
        Assert.Equal("Gạo rc5", vm.CargoTypeName);
        Assert.False(vm.IsToastVisible);
        Assert.True(string.IsNullOrWhiteSpace(vm.ToastMessage));
        Assert.DoesNotContain("tự điền", vm.StatusMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerCatalog_AdminCanDeleteCustomer()
    {
        await using var host = new DesktopTestHost(new AppSettings { DeveloperMode = true, ShowDeveloperTab = true });
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();
        var permissions = host.Provider.GetRequiredService<IUserPermissionService>();
        Assert.True(auth.TryUnlock("admin123").Success);

        var catalogVm = new CatalogViewModel(
            host.Provider.GetRequiredService<ICatalogService>(),
            host.Provider.GetRequiredService<IPrintNotificationService>(),
            permissions,
            auth);
        catalogVm.NotifyAdminPermissionsChanged();

        Assert.True(permissions.HasPermission(AdminPermission.CanDeleteCatalogItem));
        Assert.True(catalogVm.IsCatalogEditEnabled);
    }

    [Fact]
    public async Task CustomerCatalog_OperatorCannotDeleteCustomer()
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

        Assert.False(permissions.HasPermission(AdminPermission.CanDeleteCatalogItem));
        Assert.False(catalogVm.IsCatalogEditEnabled);
        Assert.Contains("Mở khóa Admin", catalogVm.CatalogAdminHint ?? string.Empty, StringComparison.Ordinal);
    }
}
