using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class VehicleUsageContextApplierTests
{
    private static VehicleUsageContext CreateContext() => new()
    {
        VehicleId = 1,
        PlateNumber = "81C-04621",
        RecentCustomerId = 10,
        RecentCustomerName = "Chị Thanh",
        RecentCargoTypeId = 20,
        RecentCargoTypeName = "Rô tươi",
        FrequentCargoTypeId = 20,
        FrequentCargoTypeName = "Rô tươi",
        FrequentCargoUsageCount = 5,
        LastUsedAt = new DateTimeOffset(2026, 6, 27, 8, 0, 0, TimeSpan.FromHours(7))
    };

    [Fact]
    public void ApplyBoth_SetsCustomerAndCargoIds()
    {
        var result = VehicleUsageContextApplier.Apply(CreateContext(), VehicleContextApplyMode.Both, null, null);

        Assert.Equal(10, result.CustomerId);
        Assert.Equal("Chị Thanh", result.CustomerName);
        Assert.Equal(20, result.CargoTypeId);
        Assert.Equal("Rô tươi", result.CargoTypeName);
    }

    [Fact]
    public void ApplyCustomerOnly_DoesNotChangeCargo()
    {
        var result = VehicleUsageContextApplier.Apply(
            CreateContext(),
            VehicleContextApplyMode.CustomerOnly,
            null,
            "Cà tươi");

        Assert.Equal("Chị Thanh", result.CustomerName);
        Assert.Null(result.CargoTypeName);
        Assert.False(result.CargoTypeChanged);
    }

    [Fact]
    public void ApplyCargoOnly_DoesNotChangeCustomer()
    {
        var result = VehicleUsageContextApplier.Apply(
            CreateContext(),
            VehicleContextApplyMode.CargoOnly,
            "Anh Bình",
            null);

        Assert.Null(result.CustomerName);
        Assert.Equal("Rô tươi", result.CargoTypeName);
        Assert.False(result.CustomerChanged);
    }

    [Fact]
    public void WithoutConfirmation_DoesNotAutoApplyContext()
    {
        var context = CreateContext();
        var result = VehicleUsageContextApplier.Apply(
            context,
            VehicleContextApplyMode.Both,
            "Anh Bình",
            "Cà tươi");

        Assert.True(result.CustomerChanged);
        Assert.True(result.CargoTypeChanged);
    }

    [Fact]
    public void ShouldHighlightApplyBoth_WhenBothFieldsEmpty()
    {
        Assert.True(VehicleUsageContextApplier.ShouldHighlightApplyBoth(null, null));
        Assert.True(VehicleUsageContextApplier.ShouldHighlightApplyBoth("  ", ""));
    }
}

public class Phase15VehicleContextRepositoryTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GetVehicleUsageContext_ReturnsRecentCustomerAndFrequentCargo()
    {
        var scope = _factory.Provider.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var cargo = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();

        var customer = await customers.UpsertAsync("Chị Thanh");
        var cargoA = await cargo.UpsertAsync("Rô tươi");
        var cargoB = await cargo.UpsertAsync("Cà tươi");
        var vehicle = await vehicles.UpsertAsync("81C-04621", customer.Id);

        await SeedTicketAsync(tickets, vehicle.Id, vehicle.PlateNumber, customer, cargoA,
            new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.FromHours(7)));
        await SeedTicketAsync(tickets, vehicle.Id, vehicle.PlateNumber, customer, cargoA,
            new DateTimeOffset(2026, 6, 25, 10, 0, 0, TimeSpan.FromHours(7)));
        await SeedTicketAsync(tickets, vehicle.Id, vehicle.PlateNumber, customer, cargoB,
            new DateTimeOffset(2026, 6, 27, 10, 0, 0, TimeSpan.FromHours(7)));

        var context = await tickets.GetVehicleUsageContextAsync("81C04621");

        Assert.NotNull(context);
        Assert.Equal("Chị Thanh", context.RecentCustomerName);
        Assert.Equal("Cà tươi", context.RecentCargoTypeName);
        Assert.Equal("Rô tươi", context.FrequentCargoTypeName);
        Assert.Equal(2, context.FrequentCargoUsageCount);
    }

    [Fact]
    public async Task VehicleAutocomplete_IncludesCustomerAndCargoContext()
    {
        var scope = _factory.Provider.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<FastEntrySearchService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var cargo = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();

        var customer = await customers.UpsertAsync("Chị Thanh");
        var cargoType = await cargo.UpsertAsync("Rô tươi");
        var vehicle = await vehicles.UpsertAsync("81C-04621", customer.Id);
        await SeedTicketAsync(tickets, vehicle.Id, vehicle.PlateNumber, customer, cargoType,
            new DateTimeOffset(2026, 6, 27, 10, 0, 0, TimeSpan.FromHours(7)));

        var items = await search.SearchVehiclesAsync("81C", null);

        var match = items.First(i => i.PrimaryText == "81C-04621");
        Assert.Contains("Chị Thanh", match.SecondaryText);
        Assert.Contains("Rô tươi", match.TertiaryText);
    }

    [Fact]
    public async Task FilterSummary_UpdatesFooterTotals()
    {
        var scope = _factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = _factory.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 1);
        await service.SaveAsync(draft);

        var result = await service.GetFilteredWithSummaryAsync(new WeighTicketFilter());
        Assert.Equal(5000m, result.TotalNetWeightKg);
        Assert.Equal(1, result.Count);
    }

    private static async Task SeedTicketAsync(
        IWeighTicketRepository repository,
        int vehicleId,
        string plate,
        Customer customer,
        CargoType cargoType,
        DateTimeOffset ticketDateTime)
    {
        var ticket = new WeighTicket
        {
            SequenceNumber = 1,
            TicketYear = ticketDateTime.Year,
            TicketMonth = ticketDateTime.Month,
            InternalCode = $"TEST-{Guid.NewGuid():N}",
            DisplayNumber = "0001/06",
            TicketDateTime = ticketDateTime,
            CustomerId = customer.Id,
            CustomerNameSnapshot = customer.Name,
            VehicleId = vehicleId,
            LicensePlateSnapshot = plate,
            CargoTypeId = cargoType.Id,
            CargoTypeNameSnapshot = cargoType.Name,
            GrossWeightGrams = 5000000,
            TareWeightGrams = 0,
            NetWeightGrams = 5000000,
            CreatedAt = ticketDateTime
        };

        await repository.AddAsync(ticket);
    }
}
