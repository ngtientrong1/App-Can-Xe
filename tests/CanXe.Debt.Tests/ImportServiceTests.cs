using CanXe.Debt.Contract;
using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class ContractTests
{
    [Fact]
    public void SerializedFile_RoundTripsWithValidChecksum()
    {
        var file = Make.File("DAKHA", Make.Ticket("2610-0001"), Make.Ticket("2610-0002", customer: "Nguyễn Văn Á"));

        var parsed = DebtExportSerializer.Deserialize(DebtExportSerializer.Serialize(file));

        Assert.NotNull(parsed);
        Assert.True(parsed.IsChecksumValid);
        Assert.Equal("Nguyễn Văn Á", parsed.Tickets[1].Customer);
    }

    [Fact]
    public void EditedFile_FailsTheChecksum()
    {
        var json = DebtExportSerializer.Serialize(Make.File("DAKHA", Make.Ticket("2610-0001", amount: 1_000_000)));

        var tampered = DebtExportSerializer.Deserialize(json.Replace("1000000", "10"));

        Assert.NotNull(tampered);
        Assert.False(tampered.IsChecksumValid);
    }

    [Fact]
    public void TicketHash_ChangesWhenThePriceChanges()
    {
        Assert.NotEqual(
            DebtExportSerializer.ComputeTicketHash(Make.Ticket("a", amount: 100)),
            DebtExportSerializer.ComputeTicketHash(Make.Ticket("a", amount: 200)));
    }
}

public sealed class ImportServiceTests
{
    private static async Task<ImportResult> Import(TestDb t, DebtExportFile file, string name = "f.json")
    {
        var result = await new ImportService(t.Db).ImportAsync(file, name, "ketoan");
        Assert.True(result.Ok, result.Error);
        return result.Value!;
    }

    [Fact]
    public async Task FirstImport_AddsTicketsAndLinksKnownCustomers()
    {
        using var t = new TestDb();
        t.AddSeason();
        var customer = t.AddCustomer("Công Ty Đại Lợi");

        var result = await Import(t, Make.File("DAKHA", Make.Ticket("a"), Make.Ticket("b", customer: "Người Lạ")));

        Assert.Equal(2, result.Added);
        Assert.Equal(1, result.UnmappedTickets);
        Assert.Equal(customer.Id, t.Db.Tickets.Single(x => x.Code == "a").CustomerId);
        Assert.Null(t.Db.Tickets.Single(x => x.Code == "b").CustomerId);
    }

    [Fact]
    public async Task ImportingTheSameFileTwice_DoesNotDuplicate()
    {
        using var t = new TestDb();
        t.AddSeason();
        var file = Make.File("DAKHA", Make.Ticket("a"), Make.Ticket("b"));

        await Import(t, file);
        var second = await Import(t, file);

        Assert.Equal(0, second.Added);
        Assert.Equal(2, second.Unchanged);
        Assert.Equal(2, await t.Db.Tickets.CountAsync());
    }

    [Fact]
    public async Task OverlappingFiles_AddOnlyNewTickets()
    {
        using var t = new TestDb();
        t.AddSeason();

        await Import(t, Make.File("DAKHA", Make.Ticket("a"), Make.Ticket("b")));
        var second = await Import(t, Make.File("DAKHA", Make.Ticket("b"), Make.Ticket("c")));

        Assert.Equal(1, second.Added);
        Assert.Equal(1, second.Unchanged);
        Assert.Equal(3, await t.Db.Tickets.CountAsync());
    }

    [Fact]
    public async Task SameCodeFromTwoStations_AreTwoDifferentTickets()
    {
        using var t = new TestDb();
        t.AddSeason();

        await Import(t, Make.File("DAKHA", Make.Ticket("2610-0001")));
        await Import(t, Make.File("DAKTO", Make.Ticket("2610-0001")));

        Assert.Equal(2, await t.Db.Tickets.CountAsync());
    }

    [Fact]
    public async Task ChangedTicket_IsUpdatedInPlace()
    {
        using var t = new TestDb();
        t.AddSeason();
        await Import(t, Make.File("DAKHA", Make.Ticket("a", amount: null)));

        var second = await Import(t, Make.File("DAKHA", Make.Ticket("a", amount: 5_000_000)));

        Assert.Equal(1, second.Updated);
        Assert.Equal(5_000_000, (await t.Db.Tickets.SingleAsync()).AmountVnd);
    }

    [Fact]
    public async Task TicketDeletedAtTheStation_IsMarkedDeletedAndLeavesTheDebt()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("Công Ty Đại Lợi");
        await Import(t, Make.File("DAKHA", Make.Ticket("a", amount: 700)));

        await Import(t, Make.File("DAKHA", Make.Ticket("a", amount: 700, deleted: true)));

        var overview = await new LedgerService(t.Db).GetOverviewAsync(season);
        Assert.True((await t.Db.Tickets.SingleAsync()).IsDeleted);
        Assert.Equal(0, overview.TotalBilled);
        Assert.Equal(customer.Id, (await t.Db.Tickets.SingleAsync()).CustomerId);
    }

    [Fact]
    public async Task DeletedTicketThatWasNeverImported_IsIgnored()
    {
        using var t = new TestDb();
        t.AddSeason();

        var result = await Import(t, Make.File("DAKHA", Make.Ticket("a", deleted: true)));

        Assert.Equal(0, result.Added);
        Assert.Empty(t.Db.Tickets);
    }

    [Fact]
    public async Task NewTicketInAClosedSeason_IsSkipped()
    {
        using var t = new TestDb();
        t.AddSeason(end: "2027-03-31", closed: true);

        var result = await Import(t, Make.File("DAKHA", Make.Ticket("a", date: "2026-11-01", amount: 100)));

        Assert.Equal(1, result.SkippedClosedSeason);
        Assert.Equal(0, await t.Db.Tickets.CountAsync());
    }

    [Fact]
    public async Task ClosedSeason_KeepsTicketsImportedBeforeItWasClosed()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        await Import(t, Make.File("DAKHA", Make.Ticket("a", date: "2026-11-01", amount: 100)));
        season.EndDate = new DateOnly(2027, 3, 31);
        season.IsClosed = true;
        t.Db.SaveChanges();

        var again = await Import(t, Make.File("DAKHA", Make.Ticket("a", date: "2026-11-01", amount: 999), Make.Ticket("b", date: "2026-11-02")));

        Assert.Equal(2, again.SkippedClosedSeason);
        Assert.Equal(100, (await t.Db.Tickets.SingleAsync()).AmountVnd);
    }

    [Fact]
    public async Task TicketOutsideAnySeason_IsStoredAndCounted()
    {
        using var t = new TestDb();
        t.AddSeason(start: "2026-10-01");

        var result = await Import(t, Make.File("DAKHA", Make.Ticket("old", date: "2026-05-01")));

        Assert.Equal(1, result.OutsideSeason);
        Assert.Equal(1, await t.Db.Tickets.CountAsync());
    }

    [Fact]
    public async Task OtherCargo_IsStoredButNotCountedAsPendingPrice()
    {
        using var t = new TestDb();
        t.AddSeason();

        var result = await Import(t, Make.File("DAKHA",
            Make.Ticket("a", cargo: "Rô Tươi", amount: null),
            Make.Ticket("b", cargo: "Cà Nhân", amount: null)));

        Assert.Equal(2, result.Added);
        Assert.Equal(1, result.PendingPrice);
    }

    [Fact]
    public async Task InvalidFiles_AreRejectedWithAReason()
    {
        using var t = new TestDb();
        var service = new ImportService(t.Db);
        var good = Make.File("DAKHA", Make.Ticket("a"));

        var wrongFormat = await service.ImportAsync(good with { Format = "other" }, "f", "u");
        var noStation = await service.ImportAsync(good with { Station = new StationInfo(" ", "x") }, "f", "u");
        var newer = await service.ImportAsync(good with { Version = 99 }, "f", "u");
        var corrupt = await service.ImportAsync(good with { Checksum = "00" }, "f", "u");
        var duplicate = Make.File("DAKHA", Make.Ticket("a"), Make.Ticket("a"));
        var dup = await service.ImportAsync(duplicate, "f", "u");

        Assert.All(new[] { wrongFormat, noStation, newer, corrupt, dup }, r => Assert.False(r.Ok));
        Assert.Contains("mã kiểm tra", corrupt.Error);
        Assert.Empty(t.Db.Tickets);
        Assert.Empty(t.Db.ImportBatches);
    }

    [Fact]
    public async Task OlderFileAndGapInDates_ProduceWarnings()
    {
        using var t = new TestDb();
        t.AddSeason();
        await Import(t, Make.File("DAKHA", new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero), Make.Ticket("a", date: "2026-10-05")));

        var stale = await Import(t, Make.File("DAKHA", new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero), Make.Ticket("b", date: "2026-10-20")));

        Assert.Contains(stale.Warnings, w => w.Contains("cũ hơn"));
        Assert.Contains(stale.Warnings, w => w.Contains("hở ngày"));
    }
}

public sealed class CustomerMappingTests
{
    [Fact]
    public async Task MappingAnUnknownName_AttachesPastTicketsAndFutureImports()
    {
        using var t = new TestDb();
        t.AddSeason();
        var customer = t.AddCustomer("Công Ty Đại Lợi");
        var import = new ImportService(t.Db);
        await import.ImportAsync(Make.File("DAKHA", Make.Ticket("a", customer: "Cty Dai Loi")), "f", "u");
        var customers = new CustomerService(t.Db);

        var unmapped = Assert.Single(await customers.GetUnmappedNamesAsync());
        await customers.MapNameToCustomerAsync(unmapped.Normalized, unmapped.DisplayName, customer.Id);
        await import.ImportAsync(Make.File("DAKTO", Make.Ticket("z", customer: "CTY DẠI LỢI")), "g", "u");

        Assert.Empty(await customers.GetUnmappedNamesAsync());
        Assert.All(t.Db.Tickets, x => Assert.Equal(customer.Id, x.CustomerId));
    }

    [Fact]
    public async Task CreatingACustomerFromAName_AttachesItsTickets()
    {
        using var t = new TestDb();
        t.AddSeason();
        await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", Make.Ticket("a", customer: "Khách Mới")), "f", "u");
        var customers = new CustomerService(t.Db);

        var created = await customers.CreateFromNameAsync("Khách Mới");

        Assert.True(created.Ok);
        Assert.Equal(created.Value!.Id, (await t.Db.Tickets.SingleAsync()).CustomerId);
    }

    [Fact]
    public async Task DuplicateCustomerNames_AreRefusedEvenWithDifferentSpelling()
    {
        using var t = new TestDb();
        t.AddCustomer("Công Ty Đại Lợi");

        var duplicate = await new CustomerService(t.Db).CreateAsync("cong ty  dai loi");

        Assert.False(duplicate.Ok);
    }

    [Fact]
    public async Task RenamingACustomer_KeepsTheOldSpellingWorkingAsAnAlias()
    {
        using var t = new TestDb();
        t.AddSeason();
        var customer = t.AddCustomer("Cty Đại Lợi");
        var customers = new CustomerService(t.Db);

        var renamed = await customers.UpdateAsync(customer.Id, "Công Ty Đại Lợi", null, null, true);
        await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", Make.Ticket("a", customer: "Cty Đại Lợi")), "f", "u");

        Assert.True(renamed.Ok);
        Assert.Equal(customer.Id, (await t.Db.Tickets.SingleAsync()).CustomerId);
    }

    [Fact]
    public async Task NameWithoutText_CannotBeMapped()
    {
        using var t = new TestDb();
        var customer = t.AddCustomer("A");

        var result = await new CustomerService(t.Db).MapNameToCustomerAsync("", "", customer.Id);

        Assert.False(result.Ok);
    }
}
