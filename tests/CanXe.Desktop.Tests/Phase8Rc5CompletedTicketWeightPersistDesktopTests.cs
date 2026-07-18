using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using CanXe.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase8Rc5CompletedTicketWeightPersistDesktopTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase8Rc5CompletedTicketWeightPersistDesktopTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CompletedTicket_AdminInlineEdit_ThenUpdate_PersistsWeights()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();
        var tickets = host.Provider.GetRequiredService<WeighTicketService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Rc5 persist";
            vm.LicensePlate = "51RC5-UI";
            vm.CargoTypeName = "Hàng rc5 ui";
            vm.UnitPriceText = "500";
            scale.SetManualWeightKg(8000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(12000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);

            var ticketId = vm.Tickets[0].Id;
            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets[0]);
            Assert.True(auth.TryUnlock("admin123").Success);
            typeof(MainViewModel)
                .GetMethod("RefreshAdminUiState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(vm, null);

            Assert.True(await vm.ApplyAdminInlineWeightKgAsync(1, 9000m));
            Assert.Contains("9000", (vm.DevWeight1Text ?? string.Empty).Replace(".", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal));
            Assert.True(vm.IsDevWeightEditUnlocked);
            Assert.Equal(WeightOverrideReasons.AdminInline, vm.WeightOverrideReasonCode);

            await vm.UpdateTicketCommand.ExecuteAsync(null);

            var detail = await tickets.GetTicketDetailAsync(ticketId);
            Assert.Equal(9000m, detail.Weight1Kg);
            Assert.Equal(12000m, detail.Weight2Kg);
            Assert.Equal(3000m, detail.NetWeightKg);

            var row = Assert.Single(vm.Tickets, t => t.Id == ticketId);
            Assert.Equal(3000m, row.NetWeightKg);
        });
    }

    [Fact]
    public async Task CompletedTicket_AdminInlineEdit_DoesNotMoveRowToTop()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        var scale = host.Provider.GetRequiredService<IScaleService>();
        var auth = host.Provider.GetRequiredService<IAdminAuthorizationService>();
        scale.SetManualMode(true);

        await _fixture.InvokeAsync(async _ =>
        {
            vm.CustomerName = "Xe1";
            vm.LicensePlate = "51RC5-A";
            scale.SetManualWeightKg(8000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(12000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
            var olderId = vm.Tickets[0].Id;

            vm.CustomerName = "Xe2";
            vm.LicensePlate = "51RC5-B";
            scale.SetManualWeightKg(7000m);
            await vm.CaptureWeight1Command.ExecuteAsync(null);
            scale.SetManualWeightKg(11000m);
            await vm.CaptureWeight2Command.ExecuteAsync(null);
            await vm.SaveCommand.ExecuteAsync(null);
            var newerId = vm.Tickets[0].Id;
            Assert.Equal(newerId, vm.Tickets[0].Id);
            Assert.Equal(olderId, vm.Tickets[1].Id);

            await vm.OpenTicketFromDoubleClickAsync(vm.Tickets.First(t => t.Id == olderId));
            Assert.True(auth.TryUnlock("admin123").Success);
            typeof(MainViewModel)
                .GetMethod("RefreshAdminUiState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(vm, null);

            Assert.True(await vm.ApplyAdminInlineWeightKgAsync(1, 9000m));
            await vm.UpdateTicketCommand.ExecuteAsync(null);

            Assert.Equal(newerId, vm.Tickets[0].Id);
            Assert.Equal(olderId, vm.Tickets[1].Id);
            Assert.Equal(3000m, vm.Tickets[1].NetWeightKg);
        });
    }
}
