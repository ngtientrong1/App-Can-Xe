using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class SeasonServiceTests
{
    [Fact]
    public async Task OnlyOneSeasonCanBeOpenAtATime()
    {
        using var t = new TestDb();
        var seasons = new SeasonService(t.Db);

        Assert.True((await seasons.CreateAsync("Mùa cà 2026", new DateOnly(2026, 10, 1))).Ok);
        Assert.False((await seasons.CreateAsync("Mùa cà 2027", new DateOnly(2027, 10, 1))).Ok);
    }

    [Fact]
    public async Task ClosingThenOpeningANewSeason_RequiresALaterStartDate()
    {
        using var t = new TestDb();
        var seasons = new SeasonService(t.Db);
        var first = (await seasons.CreateAsync("Mùa cà 2026", new DateOnly(2026, 10, 1))).Value!;

        Assert.True((await seasons.CloseAsync(first.Id, new DateOnly(2027, 3, 31))).Ok);

        Assert.False((await seasons.CreateAsync("Mùa cà 2027", new DateOnly(2027, 3, 31))).Ok);
        Assert.True((await seasons.CreateAsync("Mùa cà 2027", new DateOnly(2027, 10, 1))).Ok);
    }

    [Fact]
    public async Task OnlyTheLatestClosedSeasonCanBeReopened()
    {
        using var t = new TestDb();
        var seasons = new SeasonService(t.Db);
        var first = (await seasons.CreateAsync("2026", new DateOnly(2026, 10, 1))).Value!;
        await seasons.CloseAsync(first.Id, new DateOnly(2027, 3, 31));
        var second = (await seasons.CreateAsync("2027", new DateOnly(2027, 10, 1))).Value!;
        await seasons.CloseAsync(second.Id, new DateOnly(2028, 3, 31));

        Assert.False((await seasons.ReopenAsync(first.Id)).Ok);
        Assert.True((await seasons.ReopenAsync(second.Id)).Ok);
    }

    [Fact]
    public async Task CloseRejectsAnEndDateBeforeTheStart()
    {
        using var t = new TestDb();
        var season = t.AddSeason(start: "2026-10-01");

        Assert.False((await new SeasonService(t.Db).CloseAsync(season.Id, new DateOnly(2026, 9, 1))).Ok);
    }
}

public sealed class PaymentServiceTests
{
    private static Task<OperationResult<Payment>> Pay(TestDb t, Customer c, Season s, long amount, string date = "2026-10-25", PaymentMethod m = PaymentMethod.Cash) =>
        new PaymentService(t.Db).AddAsync(c.Id, s.Id, DateOnly.Parse(date), amount, m, null, "ketoan");

    [Fact]
    public async Task ValidPayment_IsRecorded()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("A");

        var result = await Pay(t, customer, season, 5_000_000, m: PaymentMethod.Transfer);

        Assert.True(result.Ok);
        Assert.Equal(PaymentMethod.Transfer, (await t.Db.Payments.SingleAsync()).Method);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task NonPositiveAmounts_AreRejected(long amount)
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("A");

        Assert.False((await Pay(t, customer, season, amount)).Ok);
    }

    [Fact]
    public async Task ClosedSeason_RefusesNewPayments()
    {
        using var t = new TestDb();
        var season = t.AddSeason(end: "2027-03-31", closed: true);
        var customer = t.AddCustomer("A");

        Assert.False((await Pay(t, customer, season, 1000, "2026-11-01")).Ok);
    }

    [Fact]
    public async Task DateBeforeTheSeason_IsRejected()
    {
        using var t = new TestDb();
        var season = t.AddSeason(start: "2026-10-01");
        var customer = t.AddCustomer("A");

        Assert.False((await Pay(t, customer, season, 1000, "2026-09-30")).Ok);
    }

    [Fact]
    public async Task VoidingAPayment_KeepsTheRecordAndNeedsAReason()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("A");
        var payment = (await Pay(t, customer, season, 1000)).Value!;
        var service = new PaymentService(t.Db);

        Assert.False((await service.VoidAsync(payment.Id, " ", "ketoan")).Ok);
        Assert.True((await service.VoidAsync(payment.Id, "Nhập nhầm", "ketoan")).Ok);
        Assert.False((await service.VoidAsync(payment.Id, "Lần nữa", "ketoan")).Ok);

        var saved = await t.Db.Payments.SingleAsync();
        Assert.True(saved.IsVoided);
        Assert.Equal("Nhập nhầm", saved.VoidReason);
        Assert.Equal("ketoan", saved.VoidedBy);
    }
}

public sealed class LedgerServiceTests
{
    private static async Task Import(TestDb t, params CanXe.Debt.Contract.ExportedTicket[] tickets)
    {
        var result = await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", tickets), "f", "u");
        Assert.True(result.Ok, result.Error);
    }

    [Fact]
    public async Task CustomerBuyingAtBothStations_HasOneSharedBalance()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("Công Ty Đại Lợi");
        await new ImportService(t.Db).ImportAsync(Make.File("DAKHA", Make.Ticket("1", amount: 300_000_000)), "ha.json", "u");
        await new ImportService(t.Db).ImportAsync(Make.File("DAKTO", Make.Ticket("1", amount: 200_000_000)), "to.json", "u");
        await new PaymentService(t.Db).AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 25), 150_000_000, PaymentMethod.Transfer, null, "ketoan");

        var overview = await new LedgerService(t.Db).GetOverviewAsync(season);

        var row = Assert.Single(overview.Customers);
        Assert.Equal(500_000_000, row.Billed);
        Assert.Equal(150_000_000, row.Paid);
        Assert.Equal(350_000_000, row.Balance);
        Assert.Equal(2, row.TicketCount);
    }

    [Fact]
    public async Task VoidedPayments_DoNotReduceTheDebt()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("A");
        await Import(t, Make.Ticket("1", customer: "A", amount: 1000));
        var payments = new PaymentService(t.Db);
        var payment = (await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 25), 400, PaymentMethod.Cash, null, "u")).Value!;
        await payments.VoidAsync(payment.Id, "nhầm", "u");

        var view = await new LedgerService(t.Db).GetCustomerAsync(customer.Id, season);

        Assert.Equal(1000, view!.Ledger.Balance);
        Assert.Single(view.Payments);
    }

    [Fact]
    public async Task OnlyConfiguredCargoCountsTowardDebt()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        t.AddCustomer("A");
        await Import(t,
            Make.Ticket("1", customer: "A", cargo: "Rô Tươi", amount: 1000),
            Make.Ticket("2", customer: "A", cargo: "Cà Nhân", amount: 9000));

        var ledger = new LedgerService(t.Db);
        Assert.Equal(1000, (await ledger.GetOverviewAsync(season)).TotalBilled);
        Assert.Equal(1, (await ledger.GetOverviewAsync(season)).ExcludedTickets);

        await new SettingsService(t.Db).SaveBilledCargoAsync(["Rô Tươi", "Cà Nhân"]);
        Assert.Equal(10_000, (await ledger.GetOverviewAsync(season)).TotalBilled);
    }

    [Fact]
    public async Task OverviewListsPendingAndUnmappedTickets()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        t.AddCustomer("A");
        await Import(t,
            Make.Ticket("1", customer: "A", amount: null),
            Make.Ticket("2", customer: "Người Lạ", amount: 500));

        var overview = await new LedgerService(t.Db).GetOverviewAsync(season);

        Assert.Equal(1, overview.PendingTickets);
        Assert.Equal(1, overview.UnmappedTickets);
        Assert.Equal(0, overview.TotalBilled);
    }

    [Fact]
    public async Task TicketsOutsideTheSeasonDateRange_AreLeftOut()
    {
        using var t = new TestDb();
        var season = t.AddSeason(start: "2026-10-01", end: "2027-03-31");
        t.AddCustomer("A");
        await Import(t,
            Make.Ticket("1", date: "2026-10-05", customer: "A", amount: 1000),
            Make.Ticket("2", date: "2026-09-20", customer: "A", amount: 7000));

        Assert.Equal(1000, (await new LedgerService(t.Db).GetOverviewAsync(season)).TotalBilled);
    }

    [Fact]
    public async Task CustomerView_ShowsCashAndTransferTotals()
    {
        using var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("A");
        await Import(t, Make.Ticket("1", customer: "A", amount: 10_000));
        var payments = new PaymentService(t.Db);
        await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 25), 3000, PaymentMethod.Cash, null, "u");
        await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 26), 2000, PaymentMethod.Transfer, null, "u");

        var view = await new LedgerService(t.Db).GetCustomerAsync(customer.Id, season);

        Assert.Equal(3000, view!.CashPaid);
        Assert.Equal(2000, view.TransferPaid);
        Assert.Equal(5000, view.Ledger.Balance);
        Assert.Equal(SettlementStatus.Partial, view.Tickets.Single().Settlement.Status);
    }
}

public sealed class UserServiceTests
{
    [Fact]
    public async Task CreateAndAuthenticate()
    {
        using var t = new TestDb();
        var users = new UserService(t.Db);

        var created = await users.CreateAsync("ketoan", "Kế toán", "mat-khau-123", UserRole.Accountant);

        Assert.True(created.Ok);
        Assert.NotNull(await users.AuthenticateAsync("KETOAN", "mat-khau-123"));
        Assert.Null(await users.AuthenticateAsync("ketoan", "sai"));
        Assert.Null(await users.AuthenticateAsync("khong-co", "mat-khau-123"));
    }

    [Theory]
    [InlineData("ab", "mat-khau-123")]
    [InlineData("co khoang trang", "mat-khau-123")]
    [InlineData("ketoan", "ngan")]
    public async Task WeakInput_IsRejected(string username, string password)
    {
        using var t = new TestDb();

        Assert.False((await new UserService(t.Db).CreateAsync(username, "", password, UserRole.Accountant)).Ok);
    }

    [Fact]
    public async Task DuplicateUsername_IsRejectedIgnoringCase()
    {
        using var t = new TestDb();
        var users = new UserService(t.Db);
        await users.CreateAsync("ketoan", "", "mat-khau-123", UserRole.Accountant);

        Assert.False((await users.CreateAsync("KeToan", "", "mat-khau-456", UserRole.Accountant)).Ok);
    }

    [Fact]
    public async Task DisabledUser_CannotLogIn_AndLastAdminCannotBeDisabled()
    {
        using var t = new TestDb();
        var users = new UserService(t.Db);
        var admin = (await users.CreateAsync("admin", "", "mat-khau-123", UserRole.Admin)).Value!;
        var accountant = (await users.CreateAsync("ketoan", "", "mat-khau-123", UserRole.Accountant)).Value!;

        Assert.False((await users.SetActiveAsync(admin.Id, false)).Ok);
        Assert.True((await users.SetActiveAsync(accountant.Id, false)).Ok);
        Assert.Null(await users.AuthenticateAsync("ketoan", "mat-khau-123"));
    }

    [Fact]
    public async Task ChangingOwnPassword_NeedsTheCurrentOne()
    {
        using var t = new TestDb();
        var users = new UserService(t.Db);
        var user = (await users.CreateAsync("ketoan", "", "mat-khau-123", UserRole.Accountant)).Value!;

        Assert.False((await users.ChangeOwnPasswordAsync(user.Id, "sai", "mat-khau-moi-1")).Ok);
        Assert.True((await users.ChangeOwnPasswordAsync(user.Id, "mat-khau-123", "mat-khau-moi-1")).Ok);
        Assert.NotNull(await users.AuthenticateAsync("ketoan", "mat-khau-moi-1"));
        Assert.Null(await users.AuthenticateAsync("ketoan", "mat-khau-123"));
    }
}
