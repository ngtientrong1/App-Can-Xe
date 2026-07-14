using System.IO;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase5Rc9CatalogSoftDeleteTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task DeleteCustomer_SoftDelete_RemovesFromCatalogList()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        await customers.UpsertAsync("Khách rc9 xóa");
        var rows = await catalog.ListAsync(CatalogTab.Customer, "Khách rc9 xóa");
        Assert.Single(rows);

        await catalog.HideCustomerAsync(rows[0].Id);
        var after = await catalog.ListAsync(CatalogTab.Customer, "Khách rc9 xóa");
        Assert.Empty(after);
    }

    [Fact]
    public async Task DeleteCustomer_DoesNotAffectHistoricalTicket()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc9 lịch sử" };
        scale.SetManualWeightKg(10000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(4000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var customer = await scope.ServiceProvider.GetRequiredService<ICustomerRepository>()
            .FindActiveByNormalizedNameAsync(CanXe.Domain.Services.TextNormalizer.Normalize("Khách rc9 lịch sử"));
        Assert.NotNull(customer);
        await catalog.HideCustomerAsync(customer!.Id);

        var detail = await service.GetTicketDetailAsync(save.SavedTicket!.Id);
        Assert.Equal("Khách rc9 lịch sử", detail.CustomerName);
    }

    [Fact]
    public async Task DeleteCustomer_ExcludesFromInputHistory()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var search = scope.ServiceProvider.GetRequiredService<FastEntrySearchService>();

        await customers.UpsertAsync("Khách rc9 history");
        var row = (await catalog.ListAsync(CatalogTab.Customer, "Khách rc9 history")).Single();
        await catalog.HideCustomerAsync(row.Id);

        var history = await search.SearchCustomersAsync("");
        Assert.DoesNotContain(history, i => i.PrimaryText == "Khách rc9 history");
    }
}

public sealed class Phase5Rc9ContinuationMetadataTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task SaveContinuation_EmptyFormFields_PreservesDbMetadata()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = "Khách continuation",
            DraftVehicle = "51A-99999",
            DraftCargoType = "Gạo rc9",
            DraftUnitPrice = 4200m,
            DraftNotes = "Ghi chú rc9"
        };
        scale.SetManualWeightKg(15000m);
        await service.CaptureWeightAsync(draft, 1);
        var w1Save = await service.SaveAsync(draft);
        Assert.True(w1Save.Success, w1Save.ErrorMessage);

        var continuation = await service.LoadTicketForContinuationAsync(w1Save.SavedTicket!.Id);
        continuation.DraftCustomer = null;
        continuation.DraftVehicle = null;
        continuation.DraftCargoType = null;
        continuation.DraftUnitPrice = null;
        continuation.DraftNotes = null;

        scale.SetManualWeightKg(6000m);
        await service.CaptureWeightAsync(continuation, 2);
        var w2Save = await service.SaveAsync(continuation);
        Assert.True(w2Save.Success, w2Save.ErrorMessage);

        var detail = await service.GetTicketDetailAsync(w2Save.SavedTicket!.Id);
        Assert.Equal("Khách continuation", detail.CustomerName);
        Assert.Equal("51A-99999", detail.LicensePlate);
        Assert.Equal("Gạo rc9", detail.CargoTypeName);
        Assert.Equal(4200m, detail.UnitPriceVndPerKg);
        Assert.Equal("Ghi chú rc9", detail.Notes);
        Assert.Equal(2, detail.EventCount);
    }

    [Fact]
    public async Task LoadContinuation_FirstTime_HasFullMetadata()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = "Khách load rc9",
            DraftVehicle = "51B-88888",
            DraftCargoType = "Ngô",
            DraftUnitPrice = 3500m,
            DraftNotes = "Note load"
        };
        scale.SetManualWeightKg(12000m);
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var loaded = await service.LoadTicketForContinuationAsync(save.SavedTicket!.Id);
        Assert.Equal("Khách load rc9", loaded.DraftCustomer);
        Assert.Equal("51B-88888", loaded.DraftVehicle);
        Assert.Equal("Ngô", loaded.DraftCargoType);
        Assert.Equal(3500m, loaded.DraftUnitPrice);
        Assert.Equal("Note load", loaded.DraftNotes);
        Assert.True(loaded.HasCapturedWeight1);
        Assert.True(loaded.IsWeight1LockedFromSavedTicket);
    }
}
