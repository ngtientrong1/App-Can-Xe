using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase5Rc7PartialSaveTests : IAsyncLifetime
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
    public async Task SaveAsync_PartialMetadata_WithWeight1AndWeight2_CreatesCompletedTicket(
        string? customer,
        string? plate,
        string? cargo)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = customer,
            DraftVehicle = plate,
            DraftCargoType = cargo
        };
        scale.SetManualWeightKg(18000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(9000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.Completed, save.WorkflowState);
        Assert.NotNull(save.SavedTicket);

        var detail = await service.GetTicketDetailAsync(save.SavedTicket!.Id);
        Assert.Equal(2, detail.EventCount);
        Assert.Equal(customer, detail.CustomerName);
    }

    [Fact]
    public async Task CustomerOnly_Weight1Weight2_Save_CreatesOneTicket()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var before = (await tickets.GetFilteredAsync(new WeighTicketFilter())).Count;
        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc7 only" };
        scale.SetManualWeightKg(12000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        var after = (await tickets.GetFilteredAsync(new WeighTicketFilter())).Count;
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public async Task CustomerOnly_Weight1_Save_AwaitingSecondWeigh()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách chờ lần 2" };
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.AwaitingSecondWeigh, save.WorkflowState);
    }

    [Fact]
    public async Task NullUnitPrice_DoesNotThrow()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách không giá" };
        scale.SetManualWeightKg(10000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(4000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Null(save.SavedTicket!.UnitPriceVndPerKg);
        Assert.Null(save.SavedTicket.TotalAmountVnd);
    }

    [Fact]
    public async Task EmptyOptionalFields_PrintModelMapper_DoesNotThrow()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var station = scope.ServiceProvider.GetRequiredService<StationSettingsService>();
        var printSettings = scope.ServiceProvider.GetRequiredService<PrintSettingsService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách in" };
        scale.SetManualWeightKg(9000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(3000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var detail = await service.GetTicketDetailAsync(save.SavedTicket!.Id);
        var stationDto = await station.GetStationAsync();
        var printDto = await printSettings.GetAsync();
        var model = WeighTicketPrintModelMapper.FromDetail(detail, stationDto, printDto);
        Assert.Equal("Khách in", model.CustomerName);
        Assert.Null(model.LicensePlate);
        Assert.Null(model.CargoTypeName);
    }

    [Fact]
    public async Task EmptyOptionalFields_ListRowMapper_DoesNotThrow()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách list" };
        scale.SetManualWeightKg(7000m);
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var items = await service.GetFilteredAsync(new WeighTicketFilter());
        var row = items.First(t => t.Id == save.SavedTicket!.Id);
        Assert.Equal("Khách list", row.CustomerName);
        Assert.Null(row.LicensePlate);
        Assert.Null(row.CargoTypeName);
        Assert.Equal("CHỜ CÂN LẦN 2", row.WorkflowStatusText);
    }

    [Fact]
    public async Task LearnFromTicketAsync_EmptyLicensePlate_DoesNotUpsertVehicle()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        await catalog.LearnFromTicketAsync("Khách học", "  ", null);
        var rows = await vehicles.ListCatalogAsync(null);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task LearnFromTicketAsync_EmptyCargoType_DoesNotUpsertCargo()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var cargoTypes = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();

        await catalog.LearnFromTicketAsync("Khách học", null, "   ");
        var rows = await cargoTypes.ListCatalogAsync(null);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task LearnFromTicketAsync_CustomerOnly_AddsCustomer()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        await catalog.LearnFromTicketAsync("Khách rc7 learn", null, null);
        var found = await customers.FindActiveByNormalizedNameAsync(
            TextNormalizer.Normalize("Khách rc7 learn"));
        Assert.NotNull(found);
    }
}
