using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Infrastructure;
using CanXe.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class WeighTicketServiceTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"canxe-test-{Guid.NewGuid():N}.db");
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddCanXeInfrastructure(_dbPath, Path.Combine(Path.GetTempPath(), "canxe-photos-test"));
        _provider = services.BuildServiceProvider();
        await DependencyInjection.InitializeDatabaseAsync(_provider);
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
            _provider = null!;
        }

        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); }
            catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task SaveAsync_WithSingleWeight_PersistsIncompleteTicket()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<CanXe.Application.Interfaces.IScaleService>();

        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft { CustomerName = "Công ty A" };
        await service.RecordWeightAsync(draft);

        var save = await service.SaveAsync(draft);
        Assert.True(save.Success);
        Assert.NotNull(save.SavedTicket);
        Assert.Equal(8500m, save.SavedTicket!.Weight1Kg);
        Assert.Null(save.SavedTicket.Weight2Kg);
        Assert.Equal(1, save.SavedTicket.EventCount);
    }

    [Fact]
    public async Task ContinueTicket_AddsSecondEventToSameTicket()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<CanXe.Application.Interfaces.IScaleService>();

        scale.SetManualMode(true);
        scale.SetManualWeightKg(8500m);

        var draft = new WeighTicketDraft();
        await service.RecordWeightAsync(draft);
        var firstSave = await service.SaveAsync(draft);
        Assert.True(firstSave.Success);

        var loaded = await service.LoadTicketForContinuationAsync(firstSave.SavedTicket!.Id);
        scale.SetManualWeightKg(18500m);
        var record = await service.RecordWeightAsync(loaded);
        Assert.True(record.Success);

        var secondSave = await service.SaveAsync(loaded);
        Assert.True(secondSave.Success);
        Assert.Equal(firstSave.SavedTicket.Id, secondSave.SavedTicket!.Id);
        Assert.Equal(8500m, secondSave.SavedTicket.Weight1Kg);
        Assert.Equal(18500m, secondSave.SavedTicket.Weight2Kg);
        Assert.Equal(9970m, secondSave.SavedTicket.BillableWeightKg);

        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        var ticketCount = await db.WeighTickets.CountAsync();
        var eventCount = await db.WeighEvents.CountAsync();
        Assert.Equal(1, ticketCount);
        Assert.Equal(2, eventCount);
    }

    [Fact]
    public async Task RecordWeightAsync_LocksAfterTwoEvents()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<CanXe.Application.Interfaces.IScaleService>();

        scale.SetManualMode(true);
        var draft = new WeighTicketDraft();

        scale.SetManualWeightKg(8500m);
        await service.RecordWeightAsync(draft);
        scale.SetManualWeightKg(18500m);
        await service.RecordWeightAsync(draft);

        var third = await service.RecordWeightAsync(draft);
        Assert.False(third.Success);
        Assert.True(third.IsRecordingLocked);
        Assert.Equal(2, draft.Events.Count);
    }

    [Fact]
    public async Task SaveAsync_WithoutWeights_Fails()
    {
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();

        var result = await service.SaveAsync(new WeighTicketDraft());
        Assert.False(result.Success);
    }
}
