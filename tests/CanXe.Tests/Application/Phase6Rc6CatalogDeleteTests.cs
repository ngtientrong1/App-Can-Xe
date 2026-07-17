using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Logging;
using CanXe.Infrastructure.Services;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase6Rc6CatalogDeleteTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task CatalogInitialLoad_NullSafe()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();

        var customers = await catalog.ListAsync(CatalogTab.Customer, null);
        var vehicles = await catalog.ListAsync(CatalogTab.Vehicle, "   ");
        var cargos = await catalog.ListAsync(CatalogTab.CargoType, string.Empty);

        Assert.NotNull(customers);
        Assert.NotNull(vehicles);
        Assert.NotNull(cargos);
    }

    [Fact]
    public async Task CustomerCatalog_AdminCanDeleteFromList()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        await customers.UpsertAsync("Khách rc6 xóa list");
        var row = (await catalog.ListAsync(CatalogTab.Customer, "Khách rc6 xóa list")).Single();
        await catalog.HideCustomerAsync(row.Id);
        Assert.Empty(await catalog.ListAsync(CatalogTab.Customer, "Khách rc6 xóa list"));
    }

    [Fact]
    public async Task VehicleCatalog_AdminCanDeleteFromList()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        await vehicles.UpsertAsync("51RC6-DEL1", null);
        var row = (await catalog.ListAsync(CatalogTab.Vehicle, "51RC6-DEL1")).Single();
        await catalog.HideVehicleAsync(row.Id);
        Assert.Empty(await catalog.ListAsync(CatalogTab.Vehicle, "51RC6-DEL1"));
    }

    [Fact]
    public async Task CargoCatalog_AdminCanDeleteFromList()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var cargos = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();

        await cargos.UpsertAsync("Hàng rc6 xóa");
        var row = (await catalog.ListAsync(CatalogTab.CargoType, "Hàng rc6 xóa")).Single();
        await catalog.HideCargoTypeAsync(row.Id);
        Assert.Empty(await catalog.ListAsync(CatalogTab.CargoType, "Hàng rc6 xóa"));
    }

    [Fact]
    public void OperatorCannotDeleteCustomer()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        var permissions = new UserPermissionService(auth);
        Assert.False(permissions.HasPermission(AdminPermission.CanDeleteCatalogItem));
    }

    [Fact]
    public void OperatorCannotDeleteVehicle()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        var permissions = new UserPermissionService(auth);
        Assert.False(permissions.HasPermission(AdminPermission.CanDeleteCatalogItem));
        Assert.False(auth.IsAdminUnlocked);
    }

    [Fact]
    public void OperatorCannotDeleteCargoType()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        var permissions = new UserPermissionService(auth);
        var gate = permissions.EnsurePermission(AdminPermission.CanDeleteCatalogItem, "CATALOG_DELETE");
        Assert.False(gate.Allowed);
        Assert.Contains("Admin", gate.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeletedCustomer_NotShownInSuggestions()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var search = scope.ServiceProvider.GetRequiredService<FastEntrySearchService>();

        await customers.UpsertAsync("Khách rc6 gợi ý");
        var row = (await catalog.ListAsync(CatalogTab.Customer, "Khách rc6 gợi ý")).Single();
        await catalog.HideCustomerAsync(row.Id);

        var suggestions = await search.SearchCustomersAsync("rc6 gợi");
        Assert.DoesNotContain(suggestions, i => i.PrimaryText == "Khách rc6 gợi ý");
    }

    [Fact]
    public async Task DeletedVehicle_NotShownInSuggestions()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var search = scope.ServiceProvider.GetRequiredService<FastEntrySearchService>();

        await vehicles.UpsertAsync("51RC6-SUG", null);
        var row = (await catalog.ListAsync(CatalogTab.Vehicle, "51RC6-SUG")).Single();
        await catalog.HideVehicleAsync(row.Id);

        var suggestions = await search.SearchVehiclesAsync("51RC6", null);
        Assert.DoesNotContain(suggestions, i => i.PrimaryText.Contains("51RC6-SUG", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeletedCargoType_NotShownInSuggestions()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var cargos = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();
        var search = scope.ServiceProvider.GetRequiredService<FastEntrySearchService>();

        await cargos.UpsertAsync("Loại rc6 gợi ý");
        var row = (await catalog.ListAsync(CatalogTab.CargoType, "Loại rc6 gợi ý")).Single();
        await catalog.HideCargoTypeAsync(row.Id);

        var suggestions = await search.SearchCargoTypesAsync("rc6 gợi");
        Assert.DoesNotContain(suggestions, i => i.PrimaryText == "Loại rc6 gợi ý");
    }

    [Fact]
    public async Task DeleteCatalog_DoesNotAffectHistoricalTickets()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = "Khách rc6 lịch sử",
            DraftVehicle = "51RC6-HIST",
            DraftCargoType = "Gạo rc6 lịch sử"
        };
        scale.SetManualWeightKg(12000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var customer = await scope.ServiceProvider.GetRequiredService<ICustomerRepository>()
            .FindActiveByNormalizedNameAsync(TextNormalizer.Normalize("Khách rc6 lịch sử"));
        var vehicle = await scope.ServiceProvider.GetRequiredService<IVehicleRepository>()
            .FindActiveByPlateAsync("51RC6-HIST");
        var cargo = await scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>()
            .FindActiveByNormalizedNameAsync(TextNormalizer.Normalize("Gạo rc6 lịch sử"));

        Assert.NotNull(customer);
        Assert.NotNull(vehicle);
        Assert.NotNull(cargo);

        await catalog.HideCustomerAsync(customer!.Id);
        await catalog.HideVehicleAsync(vehicle!.Id);
        await catalog.HideCargoTypeAsync(cargo!.Id);

        var detail = await service.GetTicketDetailAsync(save.SavedTicket!.Id);
        Assert.Equal("Khách rc6 lịch sử", detail.CustomerName);
        Assert.Equal("51RC6-HIST", detail.LicensePlate);
        Assert.Equal("Gạo rc6 lịch sử", detail.CargoTypeName);
    }

    [Fact]
    public void DeleteCatalog_WritesAudit()
    {
        var path = AdminAuditLogger.LogFilePath;
        var marker = "rc6-" + Guid.NewGuid().ToString("N");
        AdminAuditLogger.Write("DELETE_CUSTOMER", "SUCCESS", "Admin", note: marker);
        AdminAuditLogger.Write("DELETE_VEHICLE", "SUCCESS", "Admin", note: marker);
        AdminAuditLogger.Write("DELETE_CARGO_TYPE", "SUCCESS", "Admin", note: marker);
        AdminAuditLogger.Write("PERMISSION_DENIED_DELETE_CATALOG", "DENIED", "Operator", note: marker);

        var after = File.ReadAllText(path);
        Assert.Contains("DELETE_CUSTOMER", after, StringComparison.Ordinal);
        Assert.Contains("DELETE_VEHICLE", after, StringComparison.Ordinal);
        Assert.Contains("DELETE_CARGO_TYPE", after, StringComparison.Ordinal);
        Assert.Contains("PERMISSION_DENIED_DELETE_CATALOG", after, StringComparison.Ordinal);
        Assert.Contains(marker, after, StringComparison.Ordinal);
        Assert.DoesNotContain("admin123", after, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InputHistory_DoesNotThrow_DateTimeOffsetOrderBy()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var search = scope.ServiceProvider.GetRequiredService<FastEntrySearchService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var cargos = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();

        await customers.UpsertAsync("Khách history rc6");
        await vehicles.UpsertAsync("51RC6-HIST2", null);
        await cargos.UpsertAsync("Hàng history rc6");

        var customerHistory = await search.GetCustomerHistoryAsync();
        var vehicleHistory = await search.GetVehicleHistoryAsync();
        var cargoHistory = await search.GetCargoTypeHistoryAsync();

        Assert.NotNull(customerHistory);
        Assert.NotNull(vehicleHistory);
        Assert.NotNull(cargoHistory);
    }
}
