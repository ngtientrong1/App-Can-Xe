using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Infrastructure.Services;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase4Rc23DevDeleteTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task SoftDelete_SetsIsDeletedAndHidesFromList()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var deleteService = new TicketDeleteService(
            scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>(),
            new DeveloperAuthorizationService(new CanXe.Application.Configuration.AppSettings
            {
                DeveloperMode = true,
                DeveloperTicketEditEnabled = true
            }));
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(18000m);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách X", DraftVehicle = "51A-9" };
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(17000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);

        var ticketId = save.SavedTicket!.Id;
        var result = await deleteService.SoftDeleteAsync(ticketId);
        Assert.True(result.Success);

        var list = await service.GetFilteredWithSummaryAsync(new WeighTicketFilter { MaxResults = 200 });
        Assert.DoesNotContain(list.Items, t => t.Id == ticketId);
    }

    [Fact]
    public async Task SoftDelete_WithoutAuthorization_Fails()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var auth = new DeveloperAuthorizationService(new CanXe.Application.Configuration.AppSettings
        {
            DeveloperMode = false,
            DeveloperTicketEditEnabled = false
        });
        var deleteService = new TicketDeleteService(
            scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>(),
            auth);

        var result = await deleteService.SoftDeleteAsync(1);
        Assert.False(result.Success);
        Assert.Contains("quyền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
