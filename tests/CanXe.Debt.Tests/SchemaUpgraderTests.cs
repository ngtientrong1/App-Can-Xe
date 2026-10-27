using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class SchemaUpgraderTests
{
    private static async Task Exec(TestDb t, string sql) => await t.Db.Database.ExecuteSqlRawAsync(sql);

    /// <summary>Rebuilds the shape of the first release: none of the columns and tables added later.</summary>
    private static async Task MakeLegacy(TestDb t)
    {
        await Exec(t, "DROP INDEX IF EXISTS IX_Payments_PeriodId");
        foreach (var (table, column) in new[]
                 {
                     ("Tickets", "GrossWeightKg"), ("Tickets", "TareWeightKg"), ("Tickets", "NetWeightKg"),
                     ("Tickets", "AwaitingSecondWeigh"), ("Tickets", "FirstWeightKg"), ("Tickets", "Notes"),
                     ("Payments", "BankName"), ("Payments", "ReceivedBy"), ("Payments", "PaidBy"), ("Payments", "PeriodId")
                 })
            await Exec(t, $"ALTER TABLE \"{table}\" DROP COLUMN \"{column}\"");
        await Exec(t, "DROP TABLE Periods");
    }

    [Fact]
    public async Task CurrentDatabase_NeedsNothing()
    {
        using var t = new TestDb();

        Assert.Empty(await SchemaUpgrader.PendingChangesAsync(t.Db));
        Assert.Empty(await SchemaUpgrader.ApplyAsync(t.Db));
    }

    [Fact]
    public async Task OldDatabase_ListsWhatIsMissing()
    {
        using var t = new TestDb();
        await MakeLegacy(t);

        var pending = await SchemaUpgrader.PendingChangesAsync(t.Db);

        Assert.Contains("Tickets.AwaitingSecondWeigh", pending);
        Assert.Contains("Tickets.Notes", pending);
        Assert.Contains("Payments.PeriodId", pending);
        Assert.Contains("Payments.PaidBy", pending);
        Assert.Contains("Periods", pending);
    }

    [Fact]
    public async Task Upgrade_KeepsExistingDataAndMakesTheCurrentModelWork()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("Anh Thư");
        t.Db.Tickets.Add(new TicketRecord
        {
            StationCode = "DAKHA", Code = "202610-0040", DisplayNumber = "40/10", TicketDate = new DateOnly(2026, 10, 19),
            TicketDateTime = new DateTimeOffset(2026, 10, 19, 7, 11, 0, TimeSpan.FromHours(7)), CustomerId = customer.Id,
            CustomerNameRaw = "Anh Thư", Cargo = "Rô Tươi", BillableWeightKg = 11_964m, UnitPrice = 18_700m, AmountVnd = 223_726_800,
            ContentHash = "x"
        });
        t.Db.Payments.Add(new Payment { CustomerId = customer.Id, SeasonId = season.Id, PaidOn = new DateOnly(2026, 10, 25), AmountVnd = 100, CreatedBy = "kt" });
        await t.Db.SaveChangesAsync();
        await MakeLegacy(t);
        t.Db.ChangeTracker.Clear();

        var applied = await SchemaUpgrader.ApplyAsync(t.Db);

        Assert.NotEmpty(applied);
        Assert.Empty(await SchemaUpgrader.PendingChangesAsync(t.Db));

        var ticket = await t.Db.Tickets.SingleAsync();
        Assert.Equal(223_726_800, ticket.AmountVnd);
        Assert.False(ticket.AwaitingSecondWeigh);
        Assert.Null(ticket.Notes);
        Assert.Equal(1, await t.Db.Customers.CountAsync());
        Assert.Equal(100, (await t.Db.Payments.SingleAsync()).AmountVnd);

        // the newer parts of the model now work against the upgraded file
        t.Db.Periods.Add(new SettlementPeriod
        {
            CustomerId = customer.Id, SeasonId = season.Id, FromDate = new DateOnly(2026, 10, 1), ToDate = new DateOnly(2026, 10, 20),
            ClosedAt = DateTimeOffset.Now, ClosedBy = "kt"
        });
        await t.Db.SaveChangesAsync();
        var period = await t.Db.Periods.SingleAsync();
        t.Db.Payments.Add(new Payment { CustomerId = customer.Id, SeasonId = season.Id, PeriodId = period.Id, PaidOn = new DateOnly(2026, 10, 26), AmountVnd = 5, CreatedBy = "kt", PaidBy = "Bố", ReceivedBy = "A", BankName = "VCB" });
        ticket.Notes = "CÀ GỬI";
        ticket.AwaitingSecondWeigh = true;
        await t.Db.SaveChangesAsync();
        Assert.Equal(2, await t.Db.Payments.CountAsync());
    }

    [Fact]
    public async Task Upgrade_CanRunTwice()
    {
        using var t = new TestDb();
        await MakeLegacy(t);

        await SchemaUpgrader.ApplyAsync(t.Db);
        var second = await SchemaUpgrader.ApplyAsync(t.Db);

        Assert.Empty(second);
    }

    [Fact]
    public async Task UpgradedDatabase_AllowsOnlyOnePeriodPerSellerStart()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("A");
        await MakeLegacy(t);
        await SchemaUpgrader.ApplyAsync(t.Db);
        SettlementPeriod Make() => new()
        {
            CustomerId = customer.Id, SeasonId = season.Id, FromDate = new DateOnly(2026, 10, 1), ToDate = new DateOnly(2026, 10, 5),
            ClosedAt = DateTimeOffset.Now, ClosedBy = "kt"
        };
        t.Db.Periods.Add(Make());
        await t.Db.SaveChangesAsync();
        t.Db.Periods.Add(Make());

        await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync());
    }
}
