using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Desktop.Controls;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase6Rc1AdminUiWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase6Rc1AdminUiWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AdminBadgeVisible_WhenUnlocked()
    {
        await using var host = new DesktopTestHost(new AppSettings { DeveloperMode = true, ShowDeveloperTab = true });
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();

        await _fixture.InvokeAsync(_ =>
        {
            Assert.False(vm.IsAdminUnlocked);
            Assert.False(vm.IsAdminBadgeVisible);
            Assert.False(vm.IsManualWeighButtonVisible);
            Assert.True(auth.TryUnlock("admin123").Success);
            typeof(MainViewModel)
                .GetMethod("RefreshAdminUiState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(vm, null);
            Assert.True(vm.IsAdminUnlocked);
            Assert.True(vm.IsAdminBadgeVisible);
            Assert.False(vm.IsManualWeighButtonVisible);
            Assert.True(vm.IsInlineWeightEditEnabled);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ManualWeighButton_HiddenOrDisabled_WhenAdminLocked()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        await _fixture.InvokeAsync(_ =>
        {
            Assert.False(vm.IsManualWeighButtonVisible);
            Assert.False(vm.IsInlineWeightEditEnabled);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task DoubleClickAwaitingSecondWeigh_StillLoadsMetadataFirstTime()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "P6 meta";
            vm.LicensePlate = "51P-60001";
            scale.SetManualWeightKg(11000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets[0]);
        });

        Assert.Equal(TicketFormMode.AwaitingSecondWeigh, vm.FormMode);
        Assert.Equal("P6 meta", vm.CustomerName);
        Assert.Equal("51P-600.01", vm.LicensePlate);
        Assert.False(vm.IsTicketDirty);
    }

    [Fact]
    public async Task CatalogActions_HiddenOrDisabled_WhenAdminLocked()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        await _fixture.InvokeAsync(_ =>
        {
            Assert.False(vm.Catalog.IsCatalogEditEnabled);
            Assert.Contains("Admin", vm.Catalog.CatalogAdminHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ManualWeighButton_Hidden_WhenAdminUnlocked_Rc2()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();

        await _fixture.InvokeAsync(_ =>
        {
            Assert.True(auth.TryUnlock("admin123").Success);
            typeof(MainViewModel)
                .GetMethod("RefreshAdminUiState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(vm, null);
            Assert.False(vm.IsManualWeighButtonVisible);
            Assert.True(vm.IsInlineWeightEditEnabled);
            Assert.Contains("nhấp", vm.AdminInlineWeightHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task InlineWeightEdit_Enabled_WhenAdminUnlocked()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();

        await _fixture.InvokeAsync(_ =>
        {
            Assert.False(vm.IsManualWeighButtonVisible);
            Assert.True(auth.TryUnlock("admin123").Success);
            typeof(MainViewModel)
                .GetMethod("RefreshAdminUiState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(vm, null);
            Assert.True(vm.IsAdminUnlocked);
            Assert.False(vm.IsManualWeighButtonVisible);
            Assert.True(vm.IsInlineWeightEditEnabled);
            Assert.Contains("nhấp", vm.AdminInlineWeightHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CatalogSelection_ForOperator_StillWorks()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "P6 Catalog Op";
            scale.SetManualWeightKg(10000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(4000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            vm.ActiveSection = AppNavigationSection.Catalog;
            await vm.Catalog.InitializeAsync();
            Assert.False(vm.Catalog.IsCatalogEditEnabled);
            Assert.NotEmpty(vm.Catalog.Rows);

            vm.Catalog.SearchText = "P6 Catalog";
            await vm.Catalog.SearchCommand.ExecuteAsync(null);
            Assert.NotEmpty(vm.Catalog.Rows);
            Assert.Contains(
                vm.Catalog.Rows,
                row => row.PrimaryName.Contains("P6 Catalog", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task InputHistory_ForOperator_StillWorks()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.LicensePlate = "51P-60003";
            scale.SetManualWeightKg(10000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(4000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            var vehicleBox = new AutoCompleteTextBox();
            var ex = await Record.ExceptionAsync(() => vm.ShowFieldHistoryAsync(AutocompleteField.Vehicle, vehicleBox));
            Assert.Null(ex);
            Assert.NotEmpty(vm.VehicleSuggestions);
        });
    }

    [Fact]
    public async Task SaveW1_KeepsForm()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Keep W1 p6";
            vm.LicensePlate = "51P-60004";
            scale.SetManualWeightKg(11000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });

        Assert.Equal("Keep W1 p6", vm.CustomerName);
        Assert.Equal("51P-600.04", vm.LicensePlate);
        Assert.Equal(TicketFormMode.AwaitingSecondWeigh, vm.FormMode);
        Assert.True(vm.Weight1HasValue);
        Assert.False(vm.Weight2HasValue);
    }

    [Fact]
    public async Task SaveW2_ClearsForm()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Clear W2 p6";
            vm.LicensePlate = "51P-60005";
            scale.SetManualWeightKg(18000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(9000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });

        Assert.Null(vm.CustomerName);
        Assert.Null(vm.LicensePlate);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
        Assert.False(vm.Weight1HasValue);
        Assert.False(vm.Weight2HasValue);
    }

    [Fact]
    public async Task AdminLocked_CannotEditWeight1Inline()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        await _fixture.InvokeAsync(async _ =>
        {
            Assert.False(vm.IsInlineWeightEditEnabled);
            vm.BeginInlineWeightEditCommand.Execute(1);
            Assert.False(vm.IsWeight1InlineEditing);
            Assert.False(await vm.ApplyAdminInlineWeightKgAsync(1, 9000m));
            Assert.False(vm.Weight1HasValue);
        });
    }

    [Fact]
    public async Task AdminUnlocked_CanEditDraftWeight1Inline()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();

        await _fixture.InvokeAsync(async _ =>
        {
            Assert.True(auth.TryUnlock("admin123").Success);
            typeof(MainViewModel)
                .GetMethod("RefreshAdminUiState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(vm, null);
            Assert.True(vm.IsInlineWeightEditEnabled);
            Assert.False(vm.IsManualWeighButtonVisible);
            Assert.True(await vm.ApplyAdminInlineWeightKgAsync(1, 9570m));
            Assert.True(vm.Weight1HasValue);
            Assert.Contains("Cân tay", vm.Weight1SourceDisplay ?? string.Empty, StringComparison.Ordinal);
            Assert.True(vm.IsTicketDirty);
        });
    }

    [Fact]
    public async Task EditingCompletedWeight_SetsDirtyTrue()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Rc2 edit";
            vm.LicensePlate = "51R-20002";
            scale.SetManualWeightKg(16000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(6000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets[0]);
            Assert.True(auth.TryUnlock("admin123").Success);
            typeof(MainViewModel)
                .GetMethod("RefreshAdminUiState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(vm, null);
            Assert.True(await vm.ApplyAdminInlineWeightKgAsync(1, 17000m));
            Assert.Equal(TicketFormMode.Editing, vm.FormMode);
            Assert.True(vm.IsTicketDirty);
        });
    }

    [Fact]
    public async Task ViewingWithoutEdit_IsDirtyFalse_Rc2()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Rc2 view";
            vm.LicensePlate = "51R-20001";
            scale.SetManualWeightKg(15000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(7000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets[0]);
            Assert.Equal(TicketFormMode.Viewing, vm.FormMode);
            Assert.False(vm.IsTicketDirty);
        });
    }
}

