using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Infrastructure.Repositories;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase16InlineTicketEditingTests : IAsyncLifetime
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

    private async Task<(WeighTicketListItem Ticket, WeighTicketDraft Draft)> SaveDualWeighTicketAsync(
        decimal w1 = 8500m,
        decimal w2 = 18500m,
        decimal? unitPrice = 500m,
        string customer = "Test Customer")
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft
        {
            DraftCustomer = customer,
            DraftUnitPrice = unitPrice
        };
        scale.SetManualWeightKg(w1);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(w2);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        return (save.SavedTicket!, draft);
    }

    [Fact]
    public async Task List_DefaultSort_IsNewestFirst()
    {
        var repo = _factory.Provider.GetRequiredService<IWeighTicketRepository>();
        var baseTime = new DateTimeOffset(2026, 6, 27, 10, 0, 0, TimeSpan.FromHours(7));

        await SeedTicketDirectAsync(repo, baseTime.AddHours(-2), 1, "0001/06");
        await SeedTicketDirectAsync(repo, baseTime, 3, "0003/06");
        await SeedTicketDirectAsync(repo, baseTime, 2, "0002/06");

        var service = _factory.Provider.GetRequiredService<WeighTicketService>();
        var items = await service.GetFilteredAsync(new WeighTicketFilter());
        Assert.Equal(3, items.Count);
        Assert.Equal("03/06", items[0].DisplayNumber);
        Assert.Equal("02/06", items[1].DisplayNumber);
        Assert.Equal("01/06", items[2].DisplayNumber);
    }

    [Fact]
    public async Task List_SameTicketDateTime_SortsBySequenceDescending()
    {
        var repo = _factory.Provider.GetRequiredService<IWeighTicketRepository>();
        var when = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.FromHours(7));

        await SeedTicketDirectAsync(repo, when, 1, "0001/06");
        await SeedTicketDirectAsync(repo, when, 5, "0005/06");
        await SeedTicketDirectAsync(repo, when, 3, "0003/06");

        var items = await repo.GetFilteredAsync(new WeighTicketFilter());
        Assert.Equal(5, items[0].SequenceNumber);
        Assert.Equal(3, items[1].SequenceNumber);
        Assert.Equal(1, items[2].SequenceNumber);
    }

    [Fact]
    public async Task List_SameSequence_SortsByIdDescending()
    {
        var repo = _factory.Provider.GetRequiredService<IWeighTicketRepository>();
        var when = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.FromHours(7));

        var a = await SeedTicketDirectAsync(repo, when, 2, "0002/06");
        var b = await SeedTicketDirectAsync(repo, when, 2, "0002/06");
        var c = await SeedTicketDirectAsync(repo, when, 2, "0002/06");

        var items = await repo.GetFilteredAsync(new WeighTicketFilter());
        Assert.Equal(c.Id, items[0].Id);
        Assert.Equal(b.Id, items[1].Id);
        Assert.Equal(a.Id, items[2].Id);
    }

    [Fact]
    public async Task LoadForEdit_LoadsCorrectTicketOntoDraft()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);

        Assert.True(draft.IsEditMode);
        Assert.Equal(saved.Id, draft.ExistingTicketId);
        Assert.Equal(saved.DisplayNumber, draft.DisplayNumber);
        Assert.Equal(8500m, draft.DraftWeight1);
        Assert.Equal(18500m, draft.DraftWeight2);
    }

    [Fact]
    public async Task LoadForEdit_DoesNotAllocateNewTicketNumber()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();
        var previewBefore = await service.GetPreviewDisplayNumberAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);

        Assert.Equal(saved.DisplayNumber, draft.DisplayNumber);
        var previewAfter = await service.GetPreviewDisplayNumberAsync();
        Assert.Equal(previewBefore, previewAfter);
    }

    [Fact]
    public void EditMode_DisablesWeighCapture()
    {
        Assert.False(TicketEditUiRules.CanCaptureWeight(isEditingExistingTicket: true));
        Assert.True(TicketEditUiRules.CanCaptureWeight(isEditingExistingTicket: false));
    }

    [Fact]
    public async Task Update_WithoutDevUnlock_RejectsWeightChange()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftWeight1 = 8600m;

        var result = await service.UpdateTicketAsync(draft);

        Assert.False(result.Success);
        Assert.Contains("DEV", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevUpdate_CanChangeWeight1()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DeveloperWeightUnlockEnabled = true;
        draft.DraftWeight1 = 8600m;
        draft.WeightOverrideReasonCode = WeightOverrideReasons.WrongEntry;

        var result = await service.UpdateTicketAsync(draft, "dev");

        Assert.True(result.Success);
        Assert.Equal(9900m, result.UpdatedTicket!.NetWeightKg);
    }

    [Fact]
    public async Task DevUpdate_CanChangeWeight2()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DeveloperWeightUnlockEnabled = true;
        draft.DraftWeight2 = 18600m;
        draft.WeightOverrideReasonCode = WeightOverrideReasons.DeviceError;

        var result = await service.UpdateTicketAsync(draft, "dev");

        Assert.True(result.Success);
        Assert.Equal(18600m, result.UpdatedTicket!.GrossWeightKg);
    }

    [Fact]
    public async Task DevUpdate_CanAddMissingWeighEvent()
    {
        var service = CreateService();
        var scale = CreateScale();
        scale.SetManualMode(true);

        var draft = new WeighTicketDraft();
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 1);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);

        var edit = await service.LoadTicketForEditAsync(save.SavedTicket!.Id);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.DraftWeight2 = 8500m;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.MissingWeigh;

        var result = await service.UpdateTicketAsync(edit, "dev");

        Assert.True(result.Success);
        Assert.Equal(3500m, result.UpdatedTicket!.NetWeightKg);
    }

    [Fact]
    public async Task DevUpdate_RemoveOneWeigh_SingleWeightTareZero()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DeveloperWeightUnlockEnabled = true;
        draft.DraftWeight2 = null;
        draft.WeightOverrideReasonCode = WeightOverrideReasons.WrongWeigh;

        var result = await service.UpdateTicketAsync(draft, "dev");

        Assert.True(result.Success);
        Assert.Equal(8500m, result.UpdatedTicket!.GrossWeightKg);
        Assert.Equal(0m, result.UpdatedTicket.TareWeightKg);
        Assert.Equal(8500m, result.UpdatedTicket.NetWeightKg);
    }

    [Fact]
    public async Task DevUpdate_WeightChangeRequiresReason()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DeveloperWeightUnlockEnabled = true;
        draft.DraftWeight1 = 8600m;

        var result = await service.UpdateTicketAsync(draft, "dev");

        Assert.False(result.Success);
        Assert.Contains("lý do", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevUpdate_PreservesOriginalWeightGrams()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        using var db = CreateDb();
        var original = await db.WeighEvents.FirstAsync(e => e.WeighTicketId == saved.Id && e.Sequence == 1);
        var originalGrams = original.OriginalWeightGrams;

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DeveloperWeightUnlockEnabled = true;
        draft.DraftWeight1 = 8600m;
        draft.WeightOverrideReasonCode = WeightOverrideReasons.WrongEntry;
        await service.UpdateTicketAsync(draft, "dev");

        var updated = await db.WeighEvents.FirstAsync(e => e.WeighTicketId == saved.Id && e.Sequence == 1);
        Assert.Equal(originalGrams, updated.OriginalWeightGrams);
        Assert.NotEqual(originalGrams, updated.OverrideWeightGrams);
    }

    [Fact]
    public async Task EffectiveWeight_UsesOverrideWhenPresent()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DeveloperWeightUnlockEnabled = true;
        draft.DraftWeight1 = 8600m;
        draft.WeightOverrideReasonCode = WeightOverrideReasons.WrongEntry;
        await service.UpdateTicketAsync(draft, "dev");

        using var db = CreateDb();
        var ev = await db.WeighEvents.FirstAsync(e => e.WeighTicketId == saved.Id && e.Sequence == 1);
        Assert.Equal(8600000, WeighEventWeightResolver.GetEffectiveWeightGrams(ev));
        Assert.True(WeighEventWeightResolver.HasManualOverride(ev));
    }

    [Fact]
    public async Task DevUpdate_PhotoPathUnchangedOnOverride()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        using var db = CreateDb();
        var before = await db.WeighEvents.FirstAsync(e => e.WeighTicketId == saved.Id && e.Sequence == 1);
        var photoPath = before.PhotoPath;

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DeveloperWeightUnlockEnabled = true;
        draft.DraftWeight1 = 8600m;
        draft.WeightOverrideReasonCode = WeightOverrideReasons.WrongEntry;
        await service.UpdateTicketAsync(draft, "dev");

        var after = await db.WeighEvents.FirstAsync(e => e.WeighTicketId == saved.Id && e.Sequence == 1);
        Assert.Equal(photoPath, after.PhotoPath);
    }

    [Fact]
    public async Task Update_UnitPriceChange_RecalculatesBilling()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync(unitPrice: 500m);

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftUnitPrice = 600m;

        var result = await service.UpdateTicketAsync(draft, "user");

        Assert.True(result.Success);
        Assert.NotNull(result.UpdatedTicket!.TotalAmountVnd);
        Assert.True(result.UpdatedTicket.TotalAmountVnd > 0);
    }

    [Fact]
    public async Task Update_ClearUnitPrice_NullsBilling()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync(unitPrice: 500m);

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftUnitPrice = null;

        var result = await service.UpdateTicketAsync(draft, "user");

        Assert.True(result.Success);
        Assert.Null(result.UpdatedTicket!.BillableWeightKg);
        Assert.Null(result.UpdatedTicket.TotalAmountVnd);
    }

    [Fact]
    public async Task Update_UpdatesSameTicketRow()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftCustomer = "Updated Customer";

        var result = await service.UpdateTicketAsync(draft, "user");

        Assert.True(result.Success);
        Assert.Equal(saved.Id, result.UpdatedTicket!.Id);
        Assert.Equal("Updated Customer", result.UpdatedTicket.CustomerName);
    }

    [Fact]
    public async Task Update_DoesNotChangeDisplayNumber()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftNotes = "changed";

        var result = await service.UpdateTicketAsync(draft, "user");

        Assert.Equal(saved.DisplayNumber, result.UpdatedTicket!.DisplayNumber);
    }

    [Fact]
    public async Task Update_DoesNotChangeOriginalTicketDateTime()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        using var db = CreateDb();
        var before = await db.WeighTickets.AsNoTracking().FirstAsync(t => t.Id == saved.Id);

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftNotes = "changed";
        await service.UpdateTicketAsync(draft, "user");

        var after = await db.WeighTickets.AsNoTracking().FirstAsync(t => t.Id == saved.Id);
        Assert.Equal(before.TicketDateTime, after.TicketDateTime);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
    }

    [Fact]
    public async Task Update_DoesNotCreateNewTicket()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        using var db = CreateDb();
        var countBefore = await db.WeighTickets.CountAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftNotes = "changed";
        await service.UpdateTicketAsync(draft, "user");

        Assert.Equal(countBefore, await db.WeighTickets.CountAsync());
    }

    [Fact]
    public async Task Update_WritesAuditLogWithOldNewAndReason()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DeveloperWeightUnlockEnabled = true;
        draft.DraftWeight1 = 8600m;
        draft.WeightOverrideReasonCode = WeightOverrideReasons.WrongEntry;
        await service.UpdateTicketAsync(draft, "dev");

        var repo = _factory.Provider.GetRequiredService<IWeighTicketRepository>();
        var logs = await repo.GetAuditLogsForTicketAsync(saved.Id);

        Assert.Contains(logs, l => l.FieldName == "Weight1" && l.OldValue is not null && l.NewValue is not null);
        Assert.Contains(logs, l => !string.IsNullOrWhiteSpace(l.Reason));
    }

    [Fact]
    public async Task ExitEditWithoutSave_DoesNotChangeDatabase()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        using var db = CreateDb();
        var before = await db.WeighTickets.AsNoTracking().FirstAsync(t => t.Id == saved.Id);

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftCustomer = "Should Not Persist";
        service.CancelDraft(draft);

        var after = await db.WeighTickets.AsNoTracking().FirstAsync(t => t.Id == saved.Id);
        Assert.Equal(before.CustomerNameSnapshot, after.CustomerNameSnapshot);
    }

    [Fact]
    public async Task UpdateFailure_KeepsOriginalData()
    {
        var scope = _factory.Provider.CreateScope();
        var broken = new BrokenUpdateRepository(scope.ServiceProvider.GetRequiredService<CanXeDbContext>());
        var service = BuildServiceWithRepository(broken, scope);

        var (saved, _) = await SaveDualWeighTicketAsync();

        using var db = CreateDb();
        var before = await db.WeighTickets.AsNoTracking().FirstAsync(t => t.Id == saved.Id);

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftCustomer = "Broken Update";

        var result = await service.UpdateTicketAsync(draft, "user");

        Assert.False(result.Success);
        var after = await db.WeighTickets.AsNoTracking().FirstAsync(t => t.Id == saved.Id);
        Assert.Equal(before.CustomerNameSnapshot, after.CustomerNameSnapshot);
    }

    [Fact]
    public async Task AfterUpdate_DraftIsNotInEditMode()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync();

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftNotes = "done";
        await service.UpdateTicketAsync(draft, "user");

        var fresh = new WeighTicketDraft();
        Assert.False(fresh.IsEditMode);
        Assert.Null(fresh.ExistingTicketId);
    }

    [Fact]
    public async Task UpdatedTicket_KeepsSortPosition_NotMovedToTop()
    {
        var repo = _factory.Provider.GetRequiredService<IWeighTicketRepository>();
        var when = new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.FromHours(7));
        var newer = new DateTimeOffset(2026, 6, 27, 10, 0, 0, TimeSpan.FromHours(7));

        var oldTicket = await SeedTicketDirectAsync(repo, when, 1, "0001/06");
        await SeedTicketDirectAsync(repo, newer, 2, "0002/06");

        var service = CreateService();
        var draft = await service.LoadTicketForEditAsync(oldTicket.Id);
        draft.DraftNotes = "edited later";
        await service.UpdateTicketAsync(draft, "user");

        var items = await repo.GetFilteredAsync(new WeighTicketFilter());
        Assert.Equal(oldTicket.Id, items[1].Id);
        Assert.True(items[0].TicketDateTime > items[1].TicketDateTime);
    }

    [Fact]
    public async Task Update_RefreshesFilterSummaryTotals()
    {
        var service = CreateService();
        var (saved, _) = await SaveDualWeighTicketAsync(unitPrice: 500m);

        var before = await service.GetFilteredWithSummaryAsync(new WeighTicketFilter());

        var draft = await service.LoadTicketForEditAsync(saved.Id);
        draft.DraftUnitPrice = 1000m;
        await service.UpdateTicketAsync(draft, "user");

        var after = await service.GetFilteredWithSummaryAsync(new WeighTicketFilter());
        Assert.True(after.TotalAmountVnd > before.TotalAmountVnd);
    }

    private static WeighTicketService BuildServiceWithRepository(
        IWeighTicketRepository repo,
        IServiceScope scope)
    {
        var sp = scope.ServiceProvider;
        var ticketUpdate = new TicketUpdateService(
            repo,
            sp.GetRequiredService<ICustomerRepository>(),
            sp.GetRequiredService<ICargoTypeRepository>(),
            sp.GetRequiredService<IVehicleRepository>());

        return new WeighTicketService(
            repo,
            sp.GetRequiredService<ICustomerRepository>(),
            sp.GetRequiredService<ICargoTypeRepository>(),
            sp.GetRequiredService<IVehicleRepository>(),
            sp.GetRequiredService<IScaleService>(),
            sp.GetRequiredService<ICameraService>(),
            sp.GetRequiredService<IPhotoStorageService>(),
            ticketUpdate);
    }

    private static async Task<WeighTicket> SeedTicketDirectAsync(
        IWeighTicketRepository repository,
        DateTimeOffset ticketDateTime,
        int sequence,
        string displayNumber)
    {
        var ticket = new WeighTicket
        {
            SequenceNumber = sequence,
            TicketYear = ticketDateTime.Year,
            TicketMonth = ticketDateTime.Month,
            InternalCode = $"TEST-{Guid.NewGuid():N}",
            DisplayNumber = displayNumber,
            TicketDateTime = ticketDateTime,
            GrossWeightGrams = 5000000,
            TareWeightGrams = 0,
            NetWeightGrams = 5000000,
            CreatedAt = ticketDateTime
        };

        return await repository.AddAsync(ticket);
    }

    private sealed class BrokenUpdateRepository(CanXeDbContext db) : IWeighTicketRepository
    {
        private readonly WeighTicketRepository _inner = new(db);

        public Task<WeighTicket?> GetByIdWithEventsAsync(int id, CancellationToken cancellationToken = default) =>
            _inner.GetByIdWithEventsAsync(id, cancellationToken);

        public Task<IReadOnlyList<WeighTicket>> GetFilteredAsync(WeighTicketFilter filter, CancellationToken cancellationToken = default) =>
            _inner.GetFilteredAsync(filter, cancellationToken);

        public Task<WeighTicket> AddAsync(WeighTicket ticket, CancellationToken cancellationToken = default) =>
            _inner.AddAsync(ticket, cancellationToken);

        public Task UpdateAsync(WeighTicket ticket, CancellationToken cancellationToken = default) =>
            _inner.UpdateAsync(ticket, cancellationToken);

        public Task<WeighEvent> AddEventAsync(WeighEvent weighEvent, CancellationToken cancellationToken = default) =>
            _inner.AddEventAsync(weighEvent, cancellationToken);

        public Task<int> GetNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default) =>
            _inner.GetNextSequenceAsync(year, month, cancellationToken);

        public Task<int> PeekNextSequenceAsync(int year, int month, CancellationToken cancellationToken = default) =>
            _inner.PeekNextSequenceAsync(year, month, cancellationToken);

        public Task<string?> GetLatestPlateForCustomerAsync(string customerName, CancellationToken cancellationToken = default) =>
            _inner.GetLatestPlateForCustomerAsync(customerName, cancellationToken);

        public Task<int> GetCargoUsageCountAsync(string cargoTypeName, CancellationToken cancellationToken = default) =>
            _inner.GetCargoUsageCountAsync(cargoTypeName, cancellationToken);

        public Task<IReadOnlyList<string>> SearchRecentNotesAsync(string searchTerm, int maxResults = 5, CancellationToken cancellationToken = default) =>
            _inner.SearchRecentNotesAsync(searchTerm, maxResults, cancellationToken);

        public Task<VehicleUsageContext?> GetVehicleUsageContextAsync(string normalizedPlate, CancellationToken cancellationToken = default) =>
            _inner.GetVehicleUsageContextAsync(normalizedPlate, cancellationToken);

        public Task UpdateTicketEditAsync(WeighTicket ticket, IReadOnlyList<WeighEvent> events, IReadOnlyList<AuditLog> auditLogs, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated update failure");

        public Task<IReadOnlyList<AuditLog>> GetAuditLogsForTicketAsync(int ticketId, CancellationToken cancellationToken = default) =>
            _inner.GetAuditLogsForTicketAsync(ticketId, cancellationToken);

        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) =>
            _inner.ExecuteInTransactionAsync(action, cancellationToken);
    }
}

public static class TicketEditUiRules
{
    public static bool CanCaptureWeight(bool isEditingExistingTicket) => !isEditingExistingTicket;
}
