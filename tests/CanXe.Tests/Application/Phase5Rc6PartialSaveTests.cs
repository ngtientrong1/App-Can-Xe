using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase5Rc6PartialSaveTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Theory]
    [InlineData("Khách A", null, null)]
    [InlineData(null, "51A-12345", null)]
    [InlineData(null, null, "Gạo")]
    [InlineData(null, null, null)]
    public async Task SaveAsync_PartialMetadata_WithWeight1_CreatesTicket(
        string? customer,
        string? plate,
        string? cargo)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = customer,
            DraftVehicle = plate,
            DraftCargoType = cargo
        };
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.AwaitingSecondWeigh, save.WorkflowState);
        Assert.NotNull(save.SavedTicket);
    }

    [Fact]
    public async Task SaveAsync_ZeroKgCaptured_IsTreatedAsValidWeight1()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(0m);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách test" };
        await service.CaptureWeightAsync(draft, 1);
        Assert.True(draft.HasCapturedWeight1);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
    }

    [Fact]
    public async Task SaveAsync_NewDraft_WithWeight1AndWeight2_CreatesCompletedTicket()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách full" };
        scale.SetManualWeightKg(18000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(9000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.Completed, save.WorkflowState);

        var detail = await service.GetTicketDetailAsync(save.SavedTicket!.Id);
        Assert.Equal(2, detail.EventCount);
    }

    [Fact]
    public async Task SaveAsync_WithoutCapturedWeight1_FailsWithGuidance()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var result = await service.SaveAsync(new WeighTicketDraft { DraftCustomer = "Khách" });

        Assert.False(result.Success);
        Assert.Contains("cân lần 1", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LearnFromTicketAsync_AddsNewCatalogEntries()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        await catalog.LearnFromTicketAsync("Khách học mới", "51Z-99999", "Muối biển");

        var rows = await catalog.ListAsync(CatalogTab.Customer, "học mới");
        Assert.Contains(rows, r => r.PrimaryName.Contains("học mới", StringComparison.OrdinalIgnoreCase));

        var duplicate = await customers.FindActiveByNormalizedNameAsync(
            CanXe.Domain.Services.TextNormalizer.Normalize("Khách học mới"));
        Assert.NotNull(duplicate);
    }
}
