using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop;
using CanXe.Desktop.Controls;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using CanXe.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase5Rc10ContinuationLoadWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc10ContinuationLoadWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DoubleClickAwaitingSecondWeigh_LoadsMetadataFirstTime_VisualSync()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        var focus = new WpfUiFocusService();

        await _fixture.InvokeAsync(async _ =>
        {
            var window = new MainWindow(focus) { DataContext = vm, Width = 1366, Height = 768 };
            window.Show();
            window.UpdateLayout();

            vm.CustomerName = "Khách rc10 dc";
            vm.LicensePlate = "51E-88888";
            vm.CargoTypeName = "Ngô";
            vm.UnitPriceText = "4200";
            vm.Notes = "Ghi chú rc10";
            scale.SetManualWeightKg(12000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets[0]);
            window.UpdateLayout();

            Assert.Equal(TicketFormMode.AwaitingSecondWeigh, vm.FormMode);
            Assert.Equal("Khách rc10 dc", vm.CustomerName);
            Assert.Equal("51E-888.88", vm.LicensePlate);
            Assert.Equal("Ngô", vm.CargoTypeName);
            Assert.Equal(4200m, UnitPriceInputHelper.Parse(vm.UnitPriceText));
            Assert.Equal("Ghi chú rc10", vm.Notes);
            Assert.True(vm.Weight1HasValue);
            Assert.False(vm.IsTicketDirty);

            var customerBox = window.FindName("CustomerField") as AutoCompleteTextBox;
            var vehicleBox = window.FindName("VehicleField") as AutoCompleteTextBox;
            var cargoBox = window.FindName("CargoField") as AutoCompleteTextBox;
            Assert.NotNull(customerBox);
            Assert.NotNull(vehicleBox);
            Assert.NotNull(cargoBox);
            Assert.Equal("Khách rc10 dc", customerBox!.Text);
            Assert.Equal("51E-888.88", vehicleBox!.Text);
            Assert.Equal("Ngô", cargoBox!.Text);

            window.Close();
        });
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
            vm.CustomerName = "Khách save w2 rc10";
            vm.LicensePlate = "51F-55555";
            vm.CargoTypeName = "Lúa";
            scale.SetManualWeightKg(13000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets[0]);
            scale.SetManualWeightKg(5000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });

        var ticket = vm.Tickets[0];
        Assert.Equal("Khách save w2 rc10", ticket.CustomerName);
        Assert.Equal("51F-555.55", ticket.LicensePlate);
        Assert.Equal("Lúa", ticket.CargoTypeName);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
        Assert.Null(vm.CustomerName);
    }
}

[Collection("WpfSta")]
public sealed class Phase5Rc10FormResetWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc10FormResetWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveCompletedDraft_ResetsForm_VisualSync()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        var focus = new WpfUiFocusService();

        await _fixture.InvokeAsync(async _ =>
        {
            var window = new MainWindow(focus) { DataContext = vm, Width = 1366, Height = 768 };
            window.Show();
            window.UpdateLayout();

            vm.CustomerName = "Reset rc10";
            vm.LicensePlate = "51G-44444";
            scale.SetManualWeightKg(18000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(9000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
            window.UpdateLayout();

            Assert.Null(vm.CustomerName);
            var customerBox = window.FindName("CustomerField") as AutoCompleteTextBox;
            Assert.NotNull(customerBox);
            Assert.True(string.IsNullOrEmpty(customerBox!.Text));

            window.Close();
        });
    }

    [Fact]
    public async Task SaveFail_DoesNotResetForm()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Giữ khi fail";
            await vm.SaveCommand.ExecuteAsync(null);
        });

        Assert.Equal("Giữ khi fail", vm.CustomerName);
        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
    }
}

[Collection("WpfSta")]
public sealed class Phase5Rc10ViewingDirtyWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc10ViewingDirtyWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task OpenCompletedTicket_Viewing_IsDirtyFalse()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Xem rc10";
            scale.SetManualWeightKg(10000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(4000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets[0]);
        });

        Assert.Equal(TicketFormMode.Viewing, vm.FormMode);
        Assert.False(vm.IsTicketDirty);
    }

    [Fact]
    public async Task ExitViewing_DoesNotRequireDirtyConfirm()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Thoát rc10";
            scale.SetManualWeightKg(10000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(4000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets[0]);
            Assert.False(vm.IsTicketDirty);
            await vm.ExitViewCommand.ExecuteAsync(null);
        });

        Assert.Equal(TicketFormMode.Creating, vm.FormMode);
        Assert.Null(vm.ActiveTicketId);
    }
}

[Collection("WpfSta")]
public sealed class Phase5Rc10InputHistoryWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc10InputHistoryWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DoubleClickPlateInput_DoesNotThrow()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            var window = new MainWindow(new WpfUiFocusService())
            {
                DataContext = vm,
                Width = 1366,
                Height = 768
            };
            window.Show();
            window.UpdateLayout();

            vm.LicensePlate = "51H-12345";
            scale.SetManualWeightKg(10000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(4000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            var vehicleBox = window.FindName("VehicleField") as AutoCompleteTextBox;
            Assert.NotNull(vehicleBox);
            var ex = await Record.ExceptionAsync(() => vm.ShowFieldHistoryAsync(AutocompleteField.Vehicle, vehicleBox!));
            Assert.Null(ex);
            Assert.NotEmpty(vm.VehicleSuggestions);

            window.Close();
        });
    }

    [Fact]
    public async Task EmptyHistory_DoesNotThrow()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        await _fixture.InvokeAsync(async _ =>
        {
            var box = new AutoCompleteTextBox();
            await vm.ShowFieldHistoryAsync(AutocompleteField.Vehicle, box);
        });
    }
}
