using CanXe.Application.Configuration;
using CanXe.Application.Models;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;
using Xunit;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc23TicketSelectionTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc23TicketSelectionTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SingleClickSelection_DoesNotChangeActiveTicketId()
    {
        await _fixture.InvokeAsync(async _ =>
        {
            var host = new DesktopTestHost(new AppSettings
            {
                DeveloperMode = false,
                DeviceMode = "Simulation"
            });
            await using (host)
            {
                var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
                await vm.InitializeAsync();

                if (vm.Tickets.Count < 2)
                    return;

                var first = vm.Tickets[0];
                var second = vm.Tickets[1];
                vm.SelectedTicket = first;
                await vm.OpenTicketFromListCommand.ExecuteAsync(first);
                Assert.Equal(first.Id, vm.ActiveTicketId);

                vm.SelectedTicket = second;
                Assert.Equal(first.Id, vm.ActiveTicketId);
                Assert.Equal(TicketFormMode.Viewing, vm.FormMode);
            }
        });
    }

    [Fact]
    public async Task DoubleClickOpen_ChangesActiveTicketId()
    {
        await _fixture.InvokeAsync(async _ =>
        {
            var host = new DesktopTestHost(new AppSettings
            {
                DeveloperMode = false,
                DeviceMode = "Simulation"
            });
            await using (host)
            {
                var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
                await vm.InitializeAsync();

                if (vm.Tickets.Count < 2)
                    return;

                var second = vm.Tickets[1];
                await vm.OpenTicketFromListCommand.ExecuteAsync(second);
                Assert.Equal(second.Id, vm.ActiveTicketId);
                Assert.Equal(TicketFormMode.Viewing, vm.FormMode);
            }
        });
    }
}
