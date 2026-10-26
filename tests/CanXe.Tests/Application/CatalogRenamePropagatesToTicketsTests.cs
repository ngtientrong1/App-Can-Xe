using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class CatalogRenamePropagatesToTicketsTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task RenamingCustomerCargoAndVehicle_UpdatesExistingTickets()
    {
        await SaveTicketAsync("cty dai loi", "cat vang", "51C-123.45");

        await using (var scope = _factory.Provider.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();

            var customer = Assert.Single(await catalog.ListAsync(CatalogTab.Customer, "dai loi"));
            Assert.True((await catalog.SaveCustomerAsync(
                new CustomerCatalogEdit { Id = customer.Id, Name = "CÃ´ng Ty Äáº¡i Lá»£i" })).Success);

            var cargo = Assert.Single(await catalog.ListAsync(CatalogTab.CargoType, "cat vang"));
            Assert.True((await catalog.SaveCargoTypeAsync(
                new CargoCatalogEdit { Id = cargo.Id, Name = "CÃ¡t VÃ ng" })).Success);

            var vehicle = Assert.Single(await catalog.ListAsync(CatalogTab.Vehicle, "123"));
            Assert.True((await catalog.SaveVehicleAsync(
                new VehicleCatalogEdit { Id = vehicle.Id, LicensePlate = "51D-999.99" })).Success);
        }

        await using var verifyScope = _factory.Provider.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        var ticket = await db.WeighTickets.AsNoTracking().SingleAsync();

        Assert.Equal("CÃ´ng Ty Äáº¡i Lá»£i", ticket.CustomerNameSnapshot);
        Assert.Equal("CÃ¡t VÃ ng", ticket.CargoTypeNameSnapshot);
        Assert.Equal("51D-999.99", ticket.LicensePlateSnapshot);
        Assert.NotNull(ticket.CustomerId);
        Assert.NotNull(ticket.CargoTypeId);
        Assert.NotNull(ticket.VehicleId);
    }

    [Fact]
    public async Task RenamingOneCustomer_DoesNotTouchOtherCustomersTickets()
    {
        await SaveTicketAsync("cty dai loi", "cat vang", "51C-123.45");
        await SaveTicketAsync("cty khac", "cat vang", "51C-555.55");

        await using (var scope = _factory.Provider.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            var customer = Assert.Single(await catalog.ListAsync(CatalogTab.Customer, "dai loi"));
            Assert.True((await catalog.SaveCustomerAsync(
                new CustomerCatalogEdit { Id = customer.Id, Name = "CÃ´ng Ty Äáº¡i Lá»£i" })).Success);
        }

        await using var verifyScope = _factory.Provider.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        var names = await db.WeighTickets.AsNoTracking()
            .Select(t => t.CustomerNameSnapshot).ToListAsync();

        Assert.Contains("CÃ´ng Ty Äáº¡i Lá»£i", names);
        Assert.Contains("cty khac", names);
        Assert.DoesNotContain("cty dai loi", names);
    }

    private async Task SaveTicketAsync(string customer, string cargo, string plate)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = customer,
            DraftCargoType = cargo,
            DraftVehicle = plate
        };
        scale.SetManualWeightKg(18000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(9000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
    }
}

