using System.IO;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Repositories;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public sealed class Phase8Rc4TicketListStabilityTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void TicketList_OrdersByCreatedAtDescending_NotUpdatedAt()
    {
        var older = new DateTimeOffset(2026, 7, 17, 8, 0, 0, TimeSpan.FromHours(7));
        var newer = older.AddHours(2);

        var items = new[]
        {
            CreateTicket(id: 1, createdAt: older, updatedAt: newer.AddHours(1), sequence: 1),
            CreateTicket(id: 2, createdAt: newer, updatedAt: newer, sequence: 2)
        };

        var sorted = TicketListSorter.SortNewestFirst(
            items,
            t => t.CreatedAt != default ? t.CreatedAt : t.TicketDateTime,
            t => t.SequenceNumber,
            t => t.Id);

        Assert.Equal(2, sorted[0].Id);
        Assert.Equal(1, sorted[1].Id);
    }

    [Fact]
    public void TicketList_SaveSecondWeigh_DoesNotMoveRowToTop()
    {
        Assert.False(TicketListCollectionPolicy.TreatSavedTicketAsNewListEntry(wasContinuationSave: true));

        var newer = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.FromHours(7));
        var older = newer.AddHours(-1);
        var list = new List<(DateTimeOffset TicketDateTime, int Id)>
        {
            (newer, 2),
            (older, 1)
        };

        Assert.Equal(1, list.FindIndex(x => x.Id == 1));
    }

    [Fact]
    public void TicketList_UpdateCompletedTicket_DoesNotMoveRowToTop()
    {
        Assert.False(TicketListCollectionPolicy.TreatSavedTicketAsNewListEntry(wasContinuationSave: true));
    }

    [Fact]
    public void TicketList_NewTicket_AppearsAtTop()
    {
        Assert.True(TicketListCollectionPolicy.TreatSavedTicketAsNewListEntry(wasContinuationSave: false));
    }

    [Fact]
    public async Task TicketList_Reload_UsesStableSortKey()
    {
        var repo = _factory.Provider.GetRequiredService<IWeighTicketRepository>();
        var createdOlder = new DateTimeOffset(2026, 7, 17, 9, 0, 0, TimeSpan.FromHours(7));
        var createdNewer = createdOlder.AddHours(2);

        var older = await SeedTicketDirectAsync(repo, createdOlder, 1, "0001/07");
        var newer = await SeedTicketDirectAsync(repo, createdNewer, 2, "0002/07");

        older.UpdatedAt = createdNewer.AddHours(1);
        await repo.UpdateAsync(older);

        var items = await repo.GetFilteredAsync(new WeighTicketFilter());
        Assert.Equal(2, items.Count);
        Assert.Equal(newer.Id, items[0].Id);
        Assert.Equal(older.Id, items[1].Id);
    }

    private static WeighTicket CreateTicket(
        int id,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int sequence) =>
        new()
        {
            Id = id,
            SequenceNumber = sequence,
            TicketYear = createdAt.Year,
            TicketMonth = createdAt.Month,
            InternalCode = $"202607{sequence:D4}",
            DisplayNumber = $"{sequence:D4}/07",
            TicketDateTime = createdAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

    private static async Task<WeighTicket> SeedTicketDirectAsync(
        IWeighTicketRepository repo,
        DateTimeOffset when,
        int sequence,
        string displayNumber)
    {
        var ticket = new WeighTicket
        {
            SequenceNumber = sequence,
            TicketYear = when.Year,
            TicketMonth = when.Month,
            InternalCode = TicketNumberFormatter.FormatInternalCode(when.Year, when.Month, sequence),
            DisplayNumber = displayNumber,
            TicketDateTime = when,
            CreatedAt = when,
            CustomerNameSnapshot = "Seed",
            LicensePlateSnapshot = "51SEED-01"
        };

        return await repo.AddAsync(ticket);
    }
}

public sealed class Phase8Rc4SuggestionActiveOnlyTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task AutoFill_ByPlate_SkipsDeletedCargoType()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        scale.SetManualMode(true);

        const string plate = "51RC4-AUTOFILL";
        const string customer = "Khách rc4 auto";
        const string cargo = "Loại hàng rc4 xóa";

        var draft = new WeighTicketDraft
        {
            DraftCustomer = customer,
            DraftVehicle = plate,
            DraftCargoType = cargo
        };
        scale.SetManualWeightKg(12000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var cargoRow = (await catalog.ListAsync(CatalogTab.CargoType, cargo)).Single();
        await catalog.HideCargoTypeAsync(cargoRow.Id);

        var context = await tickets.GetVehicleUsageContextAsync(PlateNormalizer.Normalize(plate));
        Assert.NotNull(context);
        Assert.Equal(customer, context!.RecentCustomerName);
        Assert.Null(context.FrequentCargoTypeName);
        Assert.Null(context.RecentCargoTypeName);

        var applied = VehicleUsageContextApplier.Apply(context, VehicleContextApplyMode.Both, null, null);
        Assert.Equal(customer, applied.CustomerName);
        Assert.Null(applied.CargoTypeName);
    }

    [Fact]
    public async Task AutoFill_ByPlate_StillFillsActiveCustomer()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        scale.SetManualMode(true);

        const string plate = "51RC4-CUST";
        const string customer = "Khách rc4 active";

        var draft = new WeighTicketDraft
        {
            DraftCustomer = customer,
            DraftVehicle = plate,
            DraftCargoType = "Hàng rc4 active"
        };
        scale.SetManualWeightKg(12000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 2);
        Assert.True((await service.SaveAsync(draft)).Success);

        var context = await tickets.GetVehicleUsageContextAsync(PlateNormalizer.Normalize(plate));
        Assert.NotNull(context);
        Assert.Equal(customer, context!.RecentCustomerName);
    }

    [Fact]
    public async Task DeletedCargoType_DoesNotAffectHistoricalTicket()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);

        const string cargo = "Loại rc4 history";

        var draft = new WeighTicketDraft
        {
            DraftCustomer = "Khách rc4 hist",
            DraftVehicle = "51RC4-HIST",
            DraftCargoType = cargo
        };
        scale.SetManualWeightKg(12000m);
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var cargoRow = (await catalog.ListAsync(CatalogTab.CargoType, cargo)).Single();
        await catalog.HideCargoTypeAsync(cargoRow.Id);

        var detail = await service.GetTicketDetailAsync(save.SavedTicket!.Id);
        Assert.Equal(cargo, detail.CargoTypeName);
    }

    [Fact]
    public async Task DeletedCargoType_DoesNotAppearInInputHistory()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var cargos = scope.ServiceProvider.GetRequiredService<ICargoTypeRepository>();
        var search = scope.ServiceProvider.GetRequiredService<FastEntrySearchService>();

        await cargos.UpsertAsync("Loại rc4 history list");
        var row = (await catalog.ListAsync(CatalogTab.CargoType, "Loại rc4 history list")).Single();
        await catalog.HideCargoTypeAsync(row.Id);

        var history = await search.GetCargoTypeHistoryAsync();
        Assert.DoesNotContain(history, item => item.PrimaryText == "Loại rc4 history list");
    }
}

public sealed class Phase8Rc4TicketListGridLayoutTests
{
    private static string ResolveDesktopFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void TicketListGrid_ColumnWidthsAreReasonable()
    {
        var xaml = File.ReadAllText(ResolveDesktopFile("MainWindow.xaml"));
        Assert.Contains("Header=\"Số phiếu\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"76\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Biển số\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"108\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Loại hàng\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"140\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Trạng thái\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"132\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TicketListGrid_NumericColumnsCentered()
    {
        var xaml = File.ReadAllText(ResolveDesktopFile("MainWindow.xaml"));
        Assert.Contains("CenterAlignedCell", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Tổng\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Thành tiền\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Trạng thái\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TicketListGrid_HeaderCentered()
    {
        var xaml = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "CanXeDesignSystem.xaml")));
        Assert.Contains("TargetType=\"DataGridColumnHeader\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Center\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TicketListGrid_DarkModeReadable()
    {
        var xaml = File.ReadAllText(ResolveDesktopFile(Path.Combine("Themes", "CanXeDesignSystem.xaml")));
        Assert.Contains("PrimaryTextBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("DataGridHeaderBackgroundBrush", xaml, StringComparison.Ordinal);
    }
}
