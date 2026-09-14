using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class Phase5Rc8CustomerOnlySaveTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task CustomerOnly_W1W2_Save_DoesNotThrow()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc8" };
        scale.SetManualWeightKg(18000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(9000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
    }

    [Fact]
    public async Task CustomerOnly_W1W2_Save_CreatesOneTicket()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var before = (await tickets.GetFilteredAsync(new WeighTicketFilter())).Count;
        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc8 one" };
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
    public async Task CustomerOnly_W1W2_Save_CreatesTwoEvents()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc8 events" };
        scale.SetManualWeightKg(11000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(4500m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        var detail = await service.GetTicketDetailAsync(save.SavedTicket!.Id);
        Assert.Equal(2, detail.EventCount);
    }

    [Fact]
    public async Task CustomerOnly_W1W2_Save_SavesCustomerName()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc8 name" };
        scale.SetManualWeightKg(10000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(4000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal("Khách rc8 name", save.SavedTicket!.CustomerName);
    }

    [Fact]
    public async Task CustomerOnly_W1W2_Save_AllowsEmptyPlateCargoPrice()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc8 empty fields" };
        scale.SetManualWeightKg(9500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(3500m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        Assert.Null(save.SavedTicket!.LicensePlate);
        Assert.Null(save.SavedTicket.CargoTypeName);
        Assert.Null(save.SavedTicket.UnitPriceVndPerKg);
        Assert.Null(save.SavedTicket.TotalAmountVnd);
    }

    [Fact]
    public async Task CustomerOnly_W1W2_Save_CompletedAndResetsForm()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc8 completed" };
        scale.SetManualWeightKg(8000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(3000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.Completed, save.WorkflowState);
    }

    [Fact]
    public async Task CustomerOnly_W1_Save_CreatesAwaitingSecondWeigh()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc8 w1 only" };
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.AwaitingSecondWeigh, save.WorkflowState);
    }

    [Fact]
    public async Task CustomerOnly_AutoLearn_UpsertsCustomer()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        await catalog.LearnFromTicketAsync("Khách rc8 learn", null, null);
        var found = await customers.FindActiveByNormalizedNameAsync(
            TextNormalizer.Normalize("Khách rc8 learn"));
        Assert.NotNull(found);
    }

    [Fact]
    public async Task CustomerOnly_AutoLearn_DoesNotUpsertEmptyVehicle()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        await catalog.LearnFromTicketAsync("Khách rc8 no vehicle", "  ", null);
        var rows = await vehicles.ListCatalogAsync(null);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task CustomerOnly_AutoLearn_DoesNotUpsertEmptyCargo()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var cargoTypes = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();

        await catalog.LearnFromTicketAsync("Khách rc8 no cargo", null, "   ");
        var rows = await cargoTypes.ListCatalogAsync(null);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task CustomerAutoLearnFailure_DoesNotFailTicketSave()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var innerCustomers = sp.GetRequiredService<ICustomerRepository>();
        var throwingCustomers = new ThrowingUpsertCustomerRepository(innerCustomers);
        var service = new WeighTicketService(
            sp.GetRequiredService<IWeighTicketRepository>(),
            throwingCustomers,
            sp.GetRequiredService<ICargoTypeRepository>(),
            sp.GetRequiredService<IVehicleRepository>(),
            sp.GetRequiredService<IScaleService>(),
            sp.GetRequiredService<TicketUpdateService>());
        var catalog = new CatalogService(
            throwingCustomers,
            sp.GetRequiredService<IVehicleRepository>(),
            sp.GetRequiredService<ICargoTypeRepository>());
        var scale = sp.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách upsert fail" };
        scale.SetManualWeightKg(7000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(2500m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal("Khách upsert fail", save.SavedTicket!.CustomerName);

        await catalog.LearnFromTicketAsync("Khách upsert fail", null, null);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("Khach Hang")]
    [InlineData("Khach Hang C")]
    public async Task CustomerNameNumericString_DoesNotThrow(string customerName)
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = customerName };
        scale.SetManualWeightKg(6000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(2000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(customerName, save.SavedTicket!.CustomerName);
    }

    [Theory]
    [InlineData(null, "51A-99999", null)]
    [InlineData(null, null, "Gạo rc8")]
    [InlineData(null, null, null)]
    public async Task PartialMetadataRegression_W1W2_Save_StillWorks(
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
        scale.SetManualWeightKg(15000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(6000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.Completed, save.WorkflowState);
    }

    [Fact]
    public async Task FullMetadataRegression_W1W2_Save_StillWorks()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = "Khách đủ",
            DraftVehicle = "51A-12345",
            DraftCargoType = "Gạo",
            DraftUnitPrice = 5000m
        };
        scale.SetManualWeightKg(20000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(8000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal("Khách đủ", save.SavedTicket!.CustomerName);
        Assert.Equal("51A-123.45", save.SavedTicket.LicensePlate);
        Assert.Equal("Gạo", save.SavedTicket.CargoTypeName);
    }

    private sealed class ThrowingUpsertCustomerRepository : ICustomerRepository
    {
        private readonly ICustomerRepository _inner;

        public ThrowingUpsertCustomerRepository(ICustomerRepository inner) => _inner = inner;

        public Task<IReadOnlyList<Customer>> SearchAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default) =>
            _inner.SearchAsync(searchTerm, maxResults, cancellationToken);

        public Task<Customer?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default) =>
            _inner.FindByNormalizedNameAsync(normalizedName, cancellationToken);

        public Task<Customer?> FindActiveByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default) =>
            _inner.FindActiveByNormalizedNameAsync(normalizedName, cancellationToken);

        public Task<Customer> UpsertAsync(string name, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated customer upsert failure");

        public Task<IReadOnlyList<Customer>> FindSimilarAsync(string name, int maxResults = 5, CancellationToken cancellationToken = default) =>
            _inner.FindSimilarAsync(name, maxResults, cancellationToken);

        public Task<IReadOnlyList<Customer>> ListCatalogAsync(string? search, CancellationToken cancellationToken = default) =>
            _inner.ListCatalogAsync(search, cancellationToken);

        public Task<IReadOnlyList<string>> ListActiveNamesForHistoryAsync(int maxResults = 50, CancellationToken cancellationToken = default) =>
            _inner.ListActiveNamesForHistoryAsync(maxResults, cancellationToken);

        public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(id, cancellationToken);

        public Task<Customer> SaveCatalogAsync(Customer customer, CancellationToken cancellationToken = default) =>
            _inner.SaveCatalogAsync(customer, cancellationToken);

        public Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default) =>
            _inner.SoftDeleteAsync(id, cancellationToken);

        public Task<int> CountTicketUsageAsync(int customerId, CancellationToken cancellationToken = default) =>
            _inner.CountTicketUsageAsync(customerId, cancellationToken);
    }
}

public sealed class Phase5Rc8LegacyCustomerSchemaTests
{
    [Fact]
    public async Task CustomerOnly_W1W2_Save_OnLegacyCustomerSchema_DoesNotThrow()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"canxe-legacy-{Guid.NewGuid():N}.db");
        await CreateLegacyCustomerSchemaAsync(dbPath);

        await using var factory = new TestApplicationFactory(dbPath);
        await factory.InitializeAsync();
        await using var scope = factory.Provider.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Khách rc8 legacy" };
        scale.SetManualWeightKg(18000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(9000m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.Completed, save.WorkflowState);
        Assert.Equal("Khách rc8 legacy", save.SavedTicket!.CustomerName);
    }

    [Fact]
    public async Task CustomerOnly_AutoLearn_OnLegacySchema_UpsertsCustomer()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"canxe-legacy-learn-{Guid.NewGuid():N}.db");
        await CreateLegacyCustomerSchemaAsync(dbPath);

        await using var factory = new TestApplicationFactory(dbPath);
        await factory.InitializeAsync();
        await using var scope = factory.Provider.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        await catalog.LearnFromTicketAsync("5", null, null);
        var found = await customers.FindActiveByNormalizedNameAsync(TextNormalizer.Normalize("5"));
        Assert.NotNull(found);
    }

    [Fact]
    public async Task LegacySchemaUpgrade_DoesNotDuplicateCargoLastUsedAtColumn()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"canxe-legacy-upgrade-{Guid.NewGuid():N}.db");
        await CreateLegacyCustomerSchemaAsync(dbPath);

        await using var factory = new TestApplicationFactory(dbPath);
        var ex = await Record.ExceptionAsync(() => factory.InitializeAsync());
        Assert.Null(ex);
    }

    private static async Task CreateLegacyCustomerSchemaAsync(string dbPath)
    {
        var options = new DbContextOptionsBuilder<CanXeDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using var db = new CanXeDbContext(options);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                NormalizedName TEXT NOT NULL,
                LastUsedAt TEXT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE CargoTypes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                NormalizedName TEXT NOT NULL,
                LastUsedAt TEXT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE Vehicles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PlateNumber TEXT NOT NULL,
                NormalizedPlateNumber TEXT NOT NULL,
                LastUsedAt TEXT NULL,
                LastCustomerId INTEGER NULL
            );
            CREATE TABLE TicketSequences (
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                LastSequence INTEGER NOT NULL,
                PRIMARY KEY (Year, Month)
            );
            CREATE TABLE WeighTickets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SequenceNumber INTEGER NOT NULL,
                TicketYear INTEGER NOT NULL,
                TicketMonth INTEGER NOT NULL,
                InternalCode TEXT NOT NULL,
                DisplayNumber TEXT NOT NULL,
                TicketDateTime TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL,
                CustomerId INTEGER NULL,
                CustomerNameSnapshot TEXT NULL,
                VehicleId INTEGER NULL,
                LicensePlateSnapshot TEXT NULL,
                CargoTypeId INTEGER NULL,
                CargoTypeNameSnapshot TEXT NULL,
                UnitPriceVndPerKg INTEGER NULL,
                Notes TEXT NULL,
                GrossWeightGrams INTEGER NULL,
                TareWeightGrams INTEGER NULL,
                NetWeightGrams INTEGER NULL,
                DeductionWeightGrams INTEGER NULL,
                BillableWeightGrams INTEGER NULL,
                TotalAmountVnd INTEGER NULL
            );
            CREATE TABLE WeighEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                WeighTicketId INTEGER NOT NULL,
                Sequence INTEGER NOT NULL,
                WeightGrams INTEGER NOT NULL,
                RecordedAt TEXT NOT NULL,
                PhotoPath TEXT NULL,
                PhotoCaptureSucceeded INTEGER NOT NULL DEFAULT 0,
                PhotoErrorMessage TEXT NULL
            );
            """);

        await DatabaseUpgrader.UpgradeAsync(db, dbPath);
    }
}
