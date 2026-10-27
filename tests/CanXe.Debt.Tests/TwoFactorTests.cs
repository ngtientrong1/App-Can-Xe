using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class TotpTests
{
    // RFC 6238 appendix B secret "12345678901234567890" (SHA-1); the 6-digit code is the last six digits of the 8-digit values.
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Code_MatchesTheRfcTestVectors(long unixSeconds, string expected)
    {
        Assert.Equal(expected, Totp.ComputeCode(RfcSecret, Totp.StepOf(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))));
    }

    [Fact]
    public void Verify_AcceptsTheCurrentAndNeighbouringStep_ButNotFarAway()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var step = Totp.StepOf(now);

        Assert.True(Totp.Verify(RfcSecret, Totp.ComputeCode(RfcSecret, step), now, 0, out var matched));
        Assert.Equal(step, matched);
        Assert.True(Totp.Verify(RfcSecret, Totp.ComputeCode(RfcSecret, step - 1), now, 0, out _));
        Assert.True(Totp.Verify(RfcSecret, Totp.ComputeCode(RfcSecret, step + 1), now, 0, out _));
        Assert.False(Totp.Verify(RfcSecret, Totp.ComputeCode(RfcSecret, step + 5), now, 0, out _));
    }

    [Fact]
    public void Verify_RefusesACodeThatWasAlreadyUsed()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var step = Totp.StepOf(now);
        var code = Totp.ComputeCode(RfcSecret, step);

        Assert.True(Totp.Verify(RfcSecret, code, now, step - 1, out var matched));
        Assert.False(Totp.Verify(RfcSecret, code, now, matched, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("abcdef")]
    [InlineData(null)]
    public void Verify_RejectsMalformedCodes(string? code)
    {
        Assert.False(Totp.Verify(RfcSecret, code, DateTimeOffset.UtcNow, 0, out _));
    }

    [Fact]
    public void Verify_IgnoresSpacesTheAppShowsInTheCode()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var code = Totp.ComputeCode(RfcSecret, Totp.StepOf(now));

        Assert.True(Totp.Verify(RfcSecret, code.Insert(3, " "), now, 0, out _));
    }

    [Fact]
    public void Secret_RoundTripsThroughBase32_AndTheUriCarriesIt()
    {
        var secret = Totp.GenerateSecret();

        Assert.Equal(32, secret.Length);
        Assert.Equal(secret, Totp.Base32Encode(Totp.Base32Decode(secret)));
        var uri = Totp.BuildUri("Công nợ cà", "ketoan", secret);
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains($"secret={secret}", uri);
    }
}

public sealed class SecondFactorTests
{
    private static async Task<(TestDb Db, UserService Users, AppUser User)> UserWithTotp()
    {
        var t = new TestDb();
        var users = new UserService(t.Db);
        var user = (await users.CreateAsync("ketoan", "Kế toán", "mat-khau-dai-123", UserRole.Accountant)).Value!;
        return (t, users, user);
    }

    private static string CodeNow(string secret, DateTimeOffset now) => Totp.ComputeCode(secret, Totp.StepOf(now));

    [Fact]
    public async Task Setup_NeedsAnAuthenticatorCodeBeforeItTurnsOn_AndReturnsRecoveryCodes()
    {
        var (t, users, user) = await UserWithTotp();
        using var _ = t;
        var secret = (await users.BeginTotpSetupAsync(user.Id)).Value!;
        var now = DateTimeOffset.UtcNow;

        Assert.False((await users.ConfirmTotpAsync(user.Id, "000000", now)).Ok);
        Assert.False((await users.GetAsync(user.Id))!.TotpEnabled);

        var confirmed = await users.ConfirmTotpAsync(user.Id, CodeNow(secret, now), now);

        Assert.True(confirmed.Ok, confirmed.Error);
        Assert.Equal(8, confirmed.Value!.Count);
        Assert.True((await users.GetAsync(user.Id))!.TotpEnabled);
    }

    [Fact]
    public async Task SecondStep_AcceptsAFreshCodeOnce_AndRefusesAReplay()
    {
        var (t, users, user) = await UserWithTotp();
        using var _ = t;
        var secret = (await users.BeginTotpSetupAsync(user.Id)).Value!;
        var now = DateTimeOffset.UtcNow;
        await users.ConfirmTotpAsync(user.Id, CodeNow(secret, now), now);
        var later = now.AddSeconds(90);
        var code = CodeNow(secret, later);

        Assert.True(await users.VerifySecondFactorAsync(user.Id, code, later));
        Assert.False(await users.VerifySecondFactorAsync(user.Id, code, later));
        Assert.False(await users.VerifySecondFactorAsync(user.Id, "123456", later));
    }

    [Fact]
    public async Task RecoveryCode_WorksOnceForALostPhone()
    {
        var (t, users, user) = await UserWithTotp();
        using var _ = t;
        var secret = (await users.BeginTotpSetupAsync(user.Id)).Value!;
        var now = DateTimeOffset.UtcNow;
        var recovery = (await users.ConfirmTotpAsync(user.Id, CodeNow(secret, now), now)).Value!;

        Assert.True(await users.VerifySecondFactorAsync(user.Id, recovery[0].ToLowerInvariant().Replace("-", " ")));
        Assert.False(await users.VerifySecondFactorAsync(user.Id, recovery[0]));
        Assert.True(await users.VerifySecondFactorAsync(user.Id, recovery[1]));
    }

    [Fact]
    public async Task RecoveryCodes_AreStoredOnlyAsHashes()
    {
        var (t, users, user) = await UserWithTotp();
        using var _ = t;
        var secret = (await users.BeginTotpSetupAsync(user.Id)).Value!;
        var now = DateTimeOffset.UtcNow;
        var recovery = (await users.ConfirmTotpAsync(user.Id, CodeNow(secret, now), now)).Value!;

        var stored = (await t.Db.Users.SingleAsync()).RecoveryCodeHashes!;

        Assert.DoesNotContain(recovery[0].Replace("-", ""), stored);
        Assert.Equal(8, stored.Split(',').Length);
    }

    [Fact]
    public async Task Reset_TurnsTwoStepOffSoTheUserCanSignInWithJustAPassword()
    {
        var (t, users, user) = await UserWithTotp();
        using var _ = t;
        var secret = (await users.BeginTotpSetupAsync(user.Id)).Value!;
        var now = DateTimeOffset.UtcNow;
        await users.ConfirmTotpAsync(user.Id, CodeNow(secret, now), now);

        Assert.True((await users.ResetTotpAsync(user.Id)).Ok);

        var saved = await users.GetAsync(user.Id);
        Assert.False(saved!.TotpEnabled);
        Assert.Null(saved.TotpSecret);
        Assert.False(await users.VerifySecondFactorAsync(user.Id, CodeNow(secret, now.AddSeconds(120)), now.AddSeconds(120)));
    }

    [Fact]
    public async Task EnablingTwice_IsRefused_AndDisabledAccountsCannotPass()
    {
        var (t, users, user) = await UserWithTotp();
        using var _ = t;
        var secret = (await users.BeginTotpSetupAsync(user.Id)).Value!;
        var now = DateTimeOffset.UtcNow;
        await users.ConfirmTotpAsync(user.Id, CodeNow(secret, now), now);

        Assert.False((await users.BeginTotpSetupAsync(user.Id)).Ok);

        await users.SetActiveAsync(user.Id, false);
        Assert.False(await users.VerifySecondFactorAsync(user.Id, CodeNow(secret, now.AddSeconds(90)), now.AddSeconds(90)));
    }

    [Fact]
    public async Task RequiringTwoStep_IsASetting()
    {
        using var t = new TestDb();
        var settings = new SettingsService(t.Db);

        Assert.False(await settings.GetRequire2faAsync());
        await settings.SaveRequire2faAsync(true);
        Assert.True(await settings.GetRequire2faAsync());
        await settings.SaveRequire2faAsync(false);
        Assert.False(await settings.GetRequire2faAsync());
    }
}

public sealed class InternetLoginThrottleTests
{
    [Fact]
    public void WrongPasswordsFromOneAddress_DoNotLockTheSameUsernameForOthers()
    {
        var throttle = new LoginThrottle(() => new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));

        for (var i = 0; i < 5; i++)
            throttle.RegisterFailure("admin", "203.0.113.9");

        Assert.True(throttle.IsLocked("admin", "203.0.113.9"));
        Assert.False(throttle.IsLocked("admin", "198.51.100.4"));
    }

    [Fact]
    public void ManyFailuresFromOneAddress_BlockThatAddressForEveryUsername_ForFifteenMinutes()
    {
        var now = new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
        var throttle = new LoginThrottle(() => now);

        for (var i = 0; i < 15; i++)
            throttle.RegisterFailure($"user{i}", "203.0.113.9");

        Assert.True(throttle.IsLocked("somebody-else", "203.0.113.9"));
        Assert.False(throttle.IsLocked("somebody-else", "198.51.100.4"));

        now = now.AddMinutes(16);
        Assert.False(throttle.IsLocked("somebody-else", "203.0.113.9"));
    }

    [Fact]
    public void SuccessClearsTheUsersOwnCounter()
    {
        var throttle = new LoginThrottle();
        for (var i = 0; i < 4; i++)
            throttle.RegisterFailure("admin", "203.0.113.9");

        throttle.Reset("admin", "203.0.113.9");
        throttle.RegisterFailure("admin", "203.0.113.9");

        Assert.False(throttle.IsLocked("admin", "203.0.113.9"));
    }
}
