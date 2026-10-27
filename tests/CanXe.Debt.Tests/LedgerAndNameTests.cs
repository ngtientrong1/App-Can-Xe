using CanXe.Debt.Services;

namespace CanXe.Debt.Tests;

public sealed class NameNormalizerTests
{
    [Theory]
    [InlineData("Công Ty Đại Lợi", "cong ty dai loi")]
    [InlineData("  Cty. ĐẠI   LỢI ", "cty dai loi")]
    [InlineData("Nguyễn Văn A (Đắk Hà)", "nguyen van a dak ha")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("---", "")]
    public void Normalize_StripsCaseDiacriticsAndPunctuation(string? input, string expected)
    {
        Assert.Equal(expected, NameNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_TreatsDifferentSpellingsOfTheSameNameAlike()
    {
        Assert.Equal(NameNormalizer.Normalize("Công Ty Đại Lợi"), NameNormalizer.Normalize("CONG TY DAI LOI"));
    }
}

public sealed class LedgerCalculatorTests
{
    private static LedgerTicket T(int id, int day, long? amount) =>
        new(id, new DateTimeOffset(2026, 10, day, 8, 0, 0, TimeSpan.Zero), amount);

    [Fact]
    public void NoPayment_EverythingIsUnpaid()
    {
        var result = LedgerCalculator.Compute([T(1, 1, 100), T(2, 2, 200)], 0);

        Assert.Equal(300, result.Billed);
        Assert.Equal(300, result.Balance);
        Assert.All(result.Tickets, t => Assert.Equal(SettlementStatus.Unpaid, t.Status));
    }

    [Fact]
    public void PartialPayment_SettlesOldestTicketsFirstThenCarriesOver()
    {
        // 100 + 200 + 300 billed, 250 paid: ticket 1 paid, ticket 2 partly paid (150 of 200), ticket 3 untouched.
        var result = LedgerCalculator.Compute([T(1, 1, 100), T(2, 2, 200), T(3, 3, 300)], 250);

        Assert.Equal(350, result.Balance);
        Assert.Equal([SettlementStatus.Paid, SettlementStatus.Partial, SettlementStatus.Unpaid], result.Tickets.Select(t => t.Status));
        Assert.Equal(150, result.Tickets[1].Allocated);
        Assert.Equal(50, result.Tickets[1].Remaining);
    }

    [Fact]
    public void LaterTrucksAbsorbEarlierPayment()
    {
        // Customer paid ahead, then three more trucks arrive: the payment keeps eating the newer tickets.
        var first = LedgerCalculator.Compute([T(1, 1, 100)], 250);
        Assert.Equal(-150, first.Balance);
        Assert.Equal(150, first.Credit);

        var later = LedgerCalculator.Compute([T(1, 1, 100), T(2, 2, 100), T(3, 3, 100)], 250);
        Assert.Equal(50, later.Balance);
        Assert.Equal(SettlementStatus.Partial, later.Tickets[2].Status);
    }

    [Fact]
    public void OrderDoesNotDependOnInputOrder()
    {
        var result = LedgerCalculator.Compute([T(2, 5, 200), T(1, 1, 100)], 100);

        Assert.Equal(SettlementStatus.Paid, result.Tickets.Single(t => t.TicketId == 1).Status);
        Assert.Equal(SettlementStatus.Unpaid, result.Tickets.Single(t => t.TicketId == 2).Status);
    }

    [Fact]
    public void TicketsWithoutPriceArePendingAndNotBilled()
    {
        var result = LedgerCalculator.Compute([T(1, 1, null), T(2, 2, 100)], 0);

        Assert.Equal(100, result.Billed);
        Assert.Equal(SettlementStatus.Pending, result.Tickets.Single(t => t.TicketId == 1).Status);
    }

    [Fact]
    public void ExactPayment_ClearsTheBalance()
    {
        var result = LedgerCalculator.Compute([T(1, 1, 100), T(2, 2, 200)], 300);

        Assert.Equal(0, result.Balance);
        Assert.All(result.Tickets, t => Assert.Equal(SettlementStatus.Paid, t.Status));
    }

    [Fact]
    public void ZeroAmountTicket_CountsAsPaid()
    {
        var result = LedgerCalculator.Compute([T(1, 1, 0)], 0);

        Assert.Equal(SettlementStatus.Paid, result.Tickets[0].Status);
    }
}

public sealed class PasswordAndThrottleTests
{
    [Fact]
    public void Hash_VerifiesOnlyTheRightPassword()
    {
        var hash = PasswordHasher.Hash("mat-khau-dai-123");

        Assert.True(PasswordHasher.Verify("mat-khau-dai-123", hash));
        Assert.False(PasswordHasher.Verify("sai-mat-khau", hash));
        Assert.NotEqual(hash, PasswordHasher.Hash("mat-khau-dai-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v1.1.x.y")]
    [InlineData("khong-phai-hash")]
    public void Verify_RejectsMalformedHashes(string stored)
    {
        Assert.False(PasswordHasher.Verify("abc", stored));
    }

    [Fact]
    public void Throttle_LocksAfterFiveFailuresAndUnlocksAfterTenMinutes()
    {
        var now = new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
        var throttle = new LoginThrottle(() => now);

        for (var i = 0; i < 4; i++)
            throttle.RegisterFailure("KeToan");
        Assert.False(throttle.IsLocked("ketoan"));

        throttle.RegisterFailure("KeToan");
        Assert.True(throttle.IsLocked("ketoan"));

        now = now.AddMinutes(11);
        Assert.False(throttle.IsLocked("ketoan"));
    }

    [Fact]
    public void Throttle_ResetClearsFailures()
    {
        var throttle = new LoginThrottle();
        for (var i = 0; i < 5; i++)
            throttle.RegisterFailure("a");

        throttle.Reset("a");

        Assert.False(throttle.IsLocked("a"));
    }
}
