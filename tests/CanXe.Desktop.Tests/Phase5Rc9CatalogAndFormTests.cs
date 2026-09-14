using System.IO;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

public sealed class Phase5Rc9CatalogUiTests
{
    private static string CatalogViewXamlPath => ResolveCatalogViewXamlPath();

    private static string DesignSystemXamlPath => Phase5Rc4DeleteIconTests.ResolveDesignSystemXamlPath();

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
    public void CatalogView_DoesNotHaveStatusColumn()
    {
        var xaml = File.ReadAllText(CatalogViewXamlPath);
        Assert.DoesNotContain("Header=\"Trạng thái\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusText", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Content=\"SỬA\"")]
    [InlineData("Content=\"ẨN\"")]
    public void CatalogView_ActionButtons_DoNotUseTextLabels(string forbidden)
    {
        var xaml = File.ReadAllText(CatalogViewXamlPath);
        Assert.DoesNotContain(forbidden, xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogView_ActionButtons_UseIconStyles()
    {
        var xaml = File.ReadAllText(CatalogViewXamlPath);
        Assert.Contains("CanXeCatalogEditButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("CanXeCatalogDeleteButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("CanXePencilIconGeometry", File.ReadAllText(DesignSystemXamlPath), StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogView_HasTabSpecificCustomerColumn()
    {
        var xaml = File.ReadAllText(CatalogViewXamlPath);
        Assert.Contains("Header=\"Tên khách hàng\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Biển số\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Loại hàng\"", xaml, StringComparison.Ordinal);
    }
}

[Collection("WpfSta")]
public sealed class Phase5Rc9FormResetWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc9FormResetWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveCompleted_W1W2_ClearsFormFields()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách reset rc9";
            vm.LicensePlate = "51A-11111";
            vm.CargoTypeName = "Gạo";
            vm.UnitPriceText = "5000";
            scale.SetManualWeightKg(18000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(9000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });

        Assert.Null(vm.CustomerName);
        Assert.Null(vm.LicensePlate);
        Assert.Null(vm.CargoTypeName);
        Assert.Null(vm.UnitPriceText);
        Assert.Null(vm.Notes);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
        Assert.Null(vm.ActiveTicketId);
        Assert.False(vm.Weight1HasValue);
        Assert.False(vm.Weight2HasValue);
        Assert.True(vm.IsPreviewDisplayNumber);
    }

    [Fact]
    public async Task UpdateTicket_Success_ClearsFormFields()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách update rc9";
            scale.SetManualWeightKg(10000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(4000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            var ticket = vm.Tickets[0];
            await vm.OpenTicketFromListCommand.ExecuteAsync(ticket);
            await vm.UnlockViewForEditCommand.ExecuteAsync(null);
            vm.UnitPriceText = "6000";
            await vm.UpdateTicketCommand.ExecuteAsync(null);
        });

        Assert.Null(vm.CustomerName);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
        Assert.False(vm.IsEditingExistingTicket);
    }
}

[Collection("WpfSta")]
public sealed class Phase5Rc9ContinuationLoadWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc9ContinuationLoadWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DoubleClickAwaitingSecondWeigh_LoadsMetadataFirstTime()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách double-click rc9";
            vm.LicensePlate = "51C-77777";
            vm.CargoTypeName = "Đậu";
            vm.UnitPriceText = "4500";
            vm.Notes = "Note dc";
            scale.SetManualWeightKg(11000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
            await vm.OpenTicketFromListCommand.ExecuteAsync(vm.Tickets[0]);
        });

        Assert.Equal(TicketFormMode.AwaitingSecondWeigh, vm.FormMode);
        Assert.Equal("Khách double-click rc9", vm.CustomerName);
        Assert.Equal("51C-777.77", vm.LicensePlate);
        Assert.Equal("Đậu", vm.CargoTypeName);
        Assert.Equal(4500m, UnitPriceInputHelper.Parse(vm.UnitPriceText));
        Assert.Equal("Note dc", vm.Notes);
        Assert.True(vm.Weight1HasValue);
    }

    [Fact]
    public async Task SaveW2AfterFirstDoubleClick_PreservesMetadata()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Khách save w2 rc9";
            vm.LicensePlate = "51D-66666";
            vm.CargoTypeName = "Lúa";
            scale.SetManualWeightKg(13000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            await vm.OpenTicketFromListCommand.ExecuteAsync(vm.Tickets[0]);
            scale.SetManualWeightKg(5000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });

        var saved = vm.Tickets[0];
        Assert.Equal("Khách save w2 rc9", saved.CustomerName);
        Assert.Equal("51D-666.66", saved.LicensePlate);
        Assert.Equal("Lúa", saved.CargoTypeName);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
    }
}
