using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class EditPaymentTests
{
    private static async Task<(TestDb Db, PaymentService Payments, Payment Payment)> OnePayment()
    {
        var t = new TestDb();
        var season = t.AddSeason();
        var customer = t.AddCustomer("A");
        var payments = new PaymentService(t.Db);
        var payment = (await payments.AddAsync(customer.Id, season.Id, new DateOnly(2026, 10, 10), 319_017_200, PaymentMethod.Transfer,
            null, "ketoan", receivedBy: "CTY SAI TEN", bankName: "ACB")).Value!;
        return (t, payments, payment);
    }

    [Fact]
    public async Task Edit_FixesTheReceiver_AndKeepsTheOldWordingInTheLog()
    {
        var (t, payments, payment) = await OnePayment();
        using var _ = t;

        var result = await payments.EditAsync(payment.Id, payment.PaidOn, payment.AmountVnd, PaymentMethod.Transfer, "ACB", null, "CTY TNHH NONG NGHIEP ANH THUHIEP", null, "admin");

        Assert.True(result.Ok, result.Error);
        var saved = await t.Db.Payments.SingleAsync();
        Assert.Equal("CTY TNHH NONG NGHIEP ANH THUHIEP", saved.ReceivedBy);
        Assert.Equal("admin", saved.EditedBy);
        Assert.Contains("Người nhận: CTY SAI TEN → CTY TNHH NONG NGHIEP ANH THUHIEP", saved.EditLog);
        Assert.DoesNotContain("Số tiền", saved.EditLog);
    }

    [Fact]
    public async Task Edit_ChangingTheAmountMovesTheDebt_AndEachEditAddsALogLine()
    {
        var (t, payments, payment) = await OnePayment();
        using var _ = t;

        await payments.EditAsync(payment.Id, payment.PaidOn, 300_000_000, PaymentMethod.Transfer, "ACB", null, "CTY SAI TEN", null, "admin");
        await payments.EditAsync(payment.Id, payment.PaidOn.AddDays(1), 300_000_000, PaymentMethod.Cash, "ACB", null, "CTY SAI TEN", null, "ketoan");

        var saved = await t.Db.Payments.SingleAsync();
        Assert.Equal(300_000_000, saved.AmountVnd);
        Assert.Equal(PaymentMethod.Cash, saved.Method);
        Assert.Null(saved.BankName);
        Assert.Equal(2, saved.EditLog!.Split('\n').Length);
    }

    [Fact]
    public async Task Edit_WithNothingChanged_IsRefused()
    {
        var (t, payments, payment) = await OnePayment();
        using var _ = t;

        var result = await payments.EditAsync(payment.Id, payment.PaidOn, payment.AmountVnd, PaymentMethod.Transfer, "ACB", null, "CTY SAI TEN", null, "admin");

        Assert.False(result.Ok);
        Assert.Null((await t.Db.Payments.SingleAsync()).EditLog);
    }

    [Fact]
    public async Task Edit_IsRefusedForVoidedPayments_BadAmounts_DatesOutsideTheSeason_AndClosedSeasons()
    {
        var (t, payments, payment) = await OnePayment();
        using var _ = t;

        Assert.False((await payments.EditAsync(payment.Id, payment.PaidOn, 0, PaymentMethod.Transfer, "ACB", null, "x", null, "admin")).Ok);
        Assert.False((await payments.EditAsync(payment.Id, new DateOnly(2000, 1, 1), 5, PaymentMethod.Transfer, "ACB", null, "x", null, "admin")).Ok);

        var season = await t.Db.Seasons.SingleAsync();
        season.IsClosed = true;
        await t.Db.SaveChangesAsync();
        Assert.False((await payments.EditAsync(payment.Id, payment.PaidOn, 5, PaymentMethod.Transfer, "ACB", null, "x", null, "admin")).Ok);

        season.IsClosed = false;
        await t.Db.SaveChangesAsync();
        await payments.VoidAsync(payment.Id, "nhầm", "admin");
        Assert.False((await payments.EditAsync(payment.Id, payment.PaidOn, 5, PaymentMethod.Transfer, "ACB", null, "x", null, "admin")).Ok);
    }
}