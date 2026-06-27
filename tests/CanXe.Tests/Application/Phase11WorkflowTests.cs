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

public class Phase11WorkflowTests : IAsyncLifetime
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

    private CanXeDbContext CreateDb() =>
        _factory.Provider.CreateScope().ServiceProvider.GetRequiredService<CanXeDbContext>();

    [Fact]
    public async Task CaptureWeight1_MultipleTimes_OnlyLastValueUsedOnSave()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        Assert.Equal(8500m, save.SavedTicket!.Weight1Kg);
    }

    [Fact]
    public async Task CaptureWeight2_MultipleTimes_OnlyLastValueUsedOnSave()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(18000m);
        await service.CaptureWeightAsync(draft, 2);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        Assert.Equal(18500m, save.SavedTicket!.Weight2Kg);
    }

    [Fact]
    public async Task CaptureWeight_DoesNotCreateOfficialTicket()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);

        var db = CreateDb();
        Assert.Equal(0, await db.WeighTickets.CountAsync());
        Assert.Equal(0, await db.WeighEvents.CountAsync());
    }

    [Fact]
    public async Task CaptureWeight_DoesNotAppearInFilteredList()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);

        var list = await service.GetFilteredAsync(new WeighTicketFilter());
        Assert.Empty(list);
    }

    [Fact]
    public async Task CaptureWeight_CallsCameraEachUpdate()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();

        var camera = new FailingThenSucceedingCameraService();
        var scope = factory.Provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var cargo = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();
        var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        var photos = scope.ServiceProvider.GetRequiredService<IPhotoStorageService>();

        var service = new WeighTicketService(repo, customers, cargo, vehicles, scale, camera, photos);
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);
        await service.WaitForPendingPhotosAsync();
        await service.CaptureWeightAsync(draft, 1);
        await service.WaitForPendingPhotosAsync();

        Assert.Equal(2, camera.CallCount);
    }

    [Fact]
    public async Task CameraFailure_DoesNotLoseWeight()
    {
        await using var factory = new TestApplicationFactory(simulateCameraFailure: true);
        await factory.InitializeAsync();

        var service = factory.Provider.CreateScope().ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = factory.Provider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);
        await service.WaitForPendingPhotosAsync();

        Assert.Equal(8500m, draft.DraftWeight1);
        Assert.Equal(DraftPhotoStatus.Failed, draft.DraftWeight1PhotoStatus);
        Assert.Null(draft.DraftWeight1PhotoPath);
    }

    [Fact]
    public async Task CameraFailureAfterUpdate_InvalidatesOldPhoto()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();

        var camera = new SucceedThenFailCameraService();
        var scope = factory.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var service = new WeighTicketService(
            sp.GetRequiredService<IWeighTicketRepository>(),
            sp.GetRequiredService<ICustomerRepository>(),
            sp.GetRequiredService<ICargoTypeRepository>(),
            sp.GetRequiredService<IVehicleRepository>(),
            sp.GetRequiredService<IScaleService>(),
            camera,
            sp.GetRequiredService<IPhotoStorageService>());

        var scale = sp.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        await service.WaitForPendingPhotosAsync();
        Assert.Equal(DraftPhotoStatus.Valid, draft.DraftWeight1PhotoStatus);
        var firstPhoto = draft.DraftWeight1PhotoPath;

        scale.SetManualWeightKg(8600m);
        await service.CaptureWeightAsync(draft, 1);
        await service.WaitForPendingPhotosAsync();

        Assert.Equal(8600m, draft.DraftWeight1);
        Assert.Equal(DraftPhotoStatus.Failed, draft.DraftWeight1PhotoStatus);
        Assert.Null(draft.DraftWeight1PhotoPath);
        if (!string.IsNullOrEmpty(firstPhoto))
            Assert.False(File.Exists(firstPhoto));
    }

    [Fact]
    public async Task Cancel_RemovesDraft_ButNotDatabase()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);

        var newDraft = new WeighTicketDraft();
        scale.SetManualWeightKg(9000m);
        await service.CaptureWeightAsync(newDraft, 1);
        service.CancelDraft(newDraft);

        var db = CreateDb();
        Assert.Equal(1, await db.WeighTickets.CountAsync());

        var freshDraft = new WeighTicketDraft();
        Assert.Null(freshDraft.DraftWeight1);
    }

    [Fact]
    public async Task Save_WithOneWeight_Succeeds()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);

        Assert.True(save.Success);
        Assert.Equal(1, save.SavedTicket!.EventCount);
    }

    [Fact]
    public async Task Save_WithTwoWeights_ComputesFormula()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftUnitPrice = 500m };
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        Assert.Equal(9970m, save.SavedTicket!.BillableWeightKg);
        Assert.Equal(4_985_000m, save.SavedTicket.TotalAmountVnd);
    }

    [Fact]
    public async Task ContinueTicket_UpdatesSameTicket()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        var first = await service.SaveAsync(draft);

        var loaded = await service.LoadTicketForContinuationAsync(first.SavedTicket!.Id);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(loaded, 2);
        var second = await service.SaveAsync(loaded);

        Assert.Equal(first.SavedTicket.Id, second.SavedTicket!.Id);
        var db = CreateDb();
        Assert.Equal(1, await db.WeighTickets.CountAsync());
        Assert.Equal(2, await db.WeighEvents.CountAsync());
    }

    [Fact]
    public async Task Continuation_CannotOverwriteSavedWeight1()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        var saved = await service.SaveAsync(draft);

        var loaded = await service.LoadTicketForContinuationAsync(saved.SavedTicket!.Id);
        scale.SetManualWeightKg(9999m);
        var attempt = await service.CaptureWeightAsync(loaded, 1);

        Assert.False(attempt.Success);
        Assert.Equal(8500m, loaded.DraftWeight1);
    }

    [Fact]
    public void BillableWeight_RoundsHalfAwayFromZero()
    {
        static decimal NetForRawBillable(decimal raw) => raw / 0.997m;

        var net1 = NetForRawBillable(1247.2m);
        var net2 = NetForRawBillable(1247.5m);

        Assert.Equal(1247m, WeightCalculator.Calculate(0m, net1, null).BillableWeightKg);
        Assert.Equal(1248m, WeightCalculator.Calculate(0m, net2, null).BillableWeightKg);
    }

    [Fact]
    public async Task Save_WithoutUnitPrice_TotalAmountNull()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(draft, 2);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        Assert.Null(save.SavedTicket!.TotalAmountVnd);
    }

    [Fact]
    public async Task SaveFailure_KeepsDraft()
    {
        var scope = _factory.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var brokenRepo = new BrokenSaveRepository(sp.GetRequiredService<CanXeDbContext>());
        var service = new WeighTicketService(
            brokenRepo,
            sp.GetRequiredService<ICustomerRepository>(),
            sp.GetRequiredService<ICargoTypeRepository>(),
            sp.GetRequiredService<IVehicleRepository>(),
            sp.GetRequiredService<IScaleService>(),
            sp.GetRequiredService<ICameraService>(),
            sp.GetRequiredService<IPhotoStorageService>());

        var scale = sp.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.CaptureWeightAsync(draft, 1);
        var result = await service.SaveAsync(draft);

        Assert.False(result.Success);
        Assert.Equal(8500m, draft.DraftWeight1);
    }

    [Fact]
    public async Task Filter_ByCustomerName_ReturnsMatches()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft { DraftCustomer = "Công ty Alpha" };
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        await service.SaveAsync(draft);

        var list = await service.GetFilteredAsync(new WeighTicketFilter { CustomerName = "Alpha" });
        Assert.Single(list);
    }

    [Fact]
    public async Task Filter_ByMultipleFields_ReturnsMatches()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = "Công ty Beta",
            DraftVehicle = "51C-123.45",
            DraftCargoType = "Cà tươi",
            DraftUnitPrice = 500m
        };
        scale.SetManualWeightKg(8500m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(18500m);
        await service.CaptureWeightAsync(draft, 2);
        var saved = await service.SaveAsync(draft);
        Assert.True(saved.Success);

        var today = DateTimeOffset.Now.Date;
        var byDate = await service.GetFilteredAsync(new WeighTicketFilter
        {
            FromDate = today,
            ToDate = today.AddDays(1).AddTicks(-1)
        });
        Assert.Contains(byDate, x => x.Id == saved.SavedTicket!.Id);

        var byCargo = await service.GetFilteredAsync(new WeighTicketFilter { CargoTypeName = "Cà" });
        Assert.Single(byCargo);

        var byPlate = await service.GetFilteredAsync(new WeighTicketFilter { LicensePlate = "51C" });
        Assert.Single(byPlate);

        var byTicketNo = await service.GetFilteredAsync(new WeighTicketFilter
        {
            DisplayNumber = saved.SavedTicket!.DisplayNumber
        });
        Assert.Single(byTicketNo);

        var byPrice = await service.GetFilteredAsync(new WeighTicketFilter { UnitPriceVndPerKg = 500m });
        Assert.Single(byPrice);
    }

    [Fact]
    public async Task ConcurrentSave_GeneratesUniqueTicketNumbers()
    {
        var scale = CreateScale();
        scale.SetManualMode(true);

        async Task<string?> SaveOne(int weight)
        {
            using var scope = _factory.Provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
            var localScale = scope.ServiceProvider.GetRequiredService<IScaleService>();
            localScale.SetManualMode(true);
            localScale.SetManualWeightKg(weight);

            var draft = new WeighTicketDraft();
            await service.CaptureWeightAsync(draft, 1);
            var result = await service.SaveAsync(draft);
            return result.Success ? result.SavedTicket!.DisplayNumber : null;
        }

        var numbers = await Task.WhenAll(Enumerable.Range(1, 5).Select(i => SaveOne(8000 + i)));
        Assert.Equal(5, numbers.Distinct().Count());
    }

    private sealed class BrokenSaveRepository(CanXeDbContext db) : IWeighTicketRepository
    {
        private readonly CanXe.Infrastructure.Repositories.WeighTicketRepository _inner = new(db);

        public Task<WeighTicket?> GetByIdWithEventsAsync(int id, CancellationToken cancellationToken = default) =>
            _inner.GetByIdWithEventsAsync(id, cancellationToken);

        public Task<IReadOnlyList<WeighTicket>> GetFilteredAsync(WeighTicketFilter filter, CancellationToken cancellationToken = default) =>
            _inner.GetFilteredAsync(filter, cancellationToken);

        public Task<WeighTicket> AddAsync(WeighTicket ticket, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated DB failure");

        public Task UpdateAsync(WeighTicket ticket, CancellationToken cancellationToken = default) =>
            _inner.UpdateAsync(ticket, cancellationToken);

        public Task<WeighEvent> AddEventAsync(WeighEvent weighEvent, CancellationToken cancellationToken = default) =>
            _inner.AddEventAsync(weighEvent, cancellationToken);

        public Task<int> GetNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default) =>
            _inner.GetNextSequenceAsync(year, month, cancellationToken);

        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) =>
            _inner.ExecuteInTransactionAsync(action, cancellationToken);
    }
}
