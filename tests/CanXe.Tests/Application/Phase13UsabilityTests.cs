using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase13UsabilityTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private WeighTicketService CreateService() =>
        _factory.Provider.CreateScope().ServiceProvider.GetRequiredService<WeighTicketService>();

    private IScaleService CreateScale() =>
        _factory.Provider.GetRequiredService<IScaleService>();

    [Fact]
    public async Task VehicleSearch_FindsPlateWithoutSeparators()
    {
        var scope = _factory.Provider.CreateScope();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        await vehicles.UpsertAsync("81C-017.28", null);

        var results = await vehicles.SearchAsync("81c01728");
        Assert.Contains(results, v => v.PlateNumber == "81C-017.28");
    }

    [Fact]
    public async Task Save_OnlyWeight1_CreatesSingleEvent()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success);
        Assert.Equal(8500m, save.SavedTicket!.GrossWeightKg);
        Assert.Equal(0m, save.SavedTicket.TareWeightKg);
        Assert.Equal(8500m, save.SavedTicket.NetWeightKg);
    }

    [Fact]
    public async Task Save_OnlyWeight2_WithoutWeight1_FailsForNewTicket()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(9200m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);

        Assert.False(save.Success);
        Assert.Contains("cân lần 1", save.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContinueSingleWeightTicket_UpdatesSameTicket()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        var first = await service.SaveAsync(draft);
        Assert.True(first.Success);

        var loaded = await service.LoadTicketForContinuationAsync(first.SavedTicket!.Id);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(loaded, 2);
        var second = await service.SaveAsync(loaded);

        Assert.Equal(first.SavedTicket.Id, second.SavedTicket!.Id);
        Assert.Equal(10000m, second.SavedTicket.NetWeightKg);
    }

    [Fact]
    public async Task PreviewDisplayNumber_DoesNotIncrementOnCancel()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var previewBefore = await service.GetPreviewDisplayNumberAsync();

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        service.CancelDraft(draft);

        var previewAfter = await service.GetPreviewDisplayNumberAsync();
        Assert.Equal(previewBefore, previewAfter);
    }
}
