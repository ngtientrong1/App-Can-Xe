using System.Security.Cryptography;
using System.Text;
using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

/// <summary>
/// Slows down password guessing on a link that is open to the internet. Failures are counted per username AND address:
/// five wrong tries lock that username for ten minutes only for the address that made them, so a stranger cannot lock the
/// owner out of their own account, and fifteen wrong tries from one address block that address for fifteen minutes.
/// </summary>
public sealed class LoginThrottle
{
    private const int MaxPerUser = 5;
    private const int MaxPerAddress = 15;
    private static readonly TimeSpan UserLock = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AddressLock = TimeSpan.FromMinutes(15);
    private readonly Dictionary<string, (int Failures, DateTimeOffset LockedUntil)> _state = [];
    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _now;

    public LoginThrottle() : this(() => DateTimeOffset.UtcNow) { }

    public LoginThrottle(Func<DateTimeOffset> now) => _now = now;

    public bool IsLocked(string username, string? address = null)
    {
        lock (_gate)
            return IsLockedKey(UserKey(username, address)) || (!string.IsNullOrEmpty(address) && IsLockedKey(AddressKey(address)));
    }

    public void RegisterFailure(string username, string? address = null)
    {
        lock (_gate)
        {
            Register(UserKey(username, address), MaxPerUser, UserLock);
            if (!string.IsNullOrEmpty(address))
                Register(AddressKey(address), MaxPerAddress, AddressLock);
        }
    }

    public void Reset(string username, string? address = null)
    {
        lock (_gate)
            _state.Remove(UserKey(username, address));
    }

    private bool IsLockedKey(string key) => _state.TryGetValue(key, out var s) && s.LockedUntil > _now();

    private void Register(string key, int max, TimeSpan lockFor)
    {
        _state.TryGetValue(key, out var s);
        if (s.LockedUntil > _now())
            return;

        var failures = s.Failures + 1;
        _state[key] = failures >= max ? (0, _now() + lockFor) : (failures, DateTimeOffset.MinValue);
    }

    private static string UserKey(string username, string? address) => $"u:{username.Trim().ToLowerInvariant()}|{address}";

    private static string AddressKey(string? address) => $"ip:{address}";
}
public sealed class UserService(DebtDbContext db)
{
    public Task<bool> AnyUserAsync(CancellationToken ct = default) => db.Users.AnyAsync(ct);

    public Task<List<AppUser>> GetAllAsync(CancellationToken ct = default) =>
        db.Users.AsNoTracking().OrderBy(u => u.Role).ThenBy(u => u.Username).ToListAsync(ct);

    public async Task<AppUser?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username.Trim(), ct);
        if (user is null)
        {
            // Spend the same time as a real check so a missing username is not obvious from the response time.
            PasswordHasher.Verify(password, DummyHash);
            return null;
        }

        return user.IsActive && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public async Task<OperationResult<AppUser>> CreateAsync(
        string username, string displayName, string password, UserRole role, CancellationToken ct = default)
    {
        username = username.Trim();
        var error = ValidateUsername(username) ?? ValidatePassword(password);
        if (error is not null)
            return OperationResult<AppUser>.Fail(error);
        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            return OperationResult<AppUser>.Fail("Tên đăng nhập này đã có.");

        var user = new AppUser
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            Role = role,
            CreatedAt = DateTimeOffset.Now
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return OperationResult<AppUser>.Success(user);
    }

    public async Task<OperationResult> SetPasswordAsync(int userId, string newPassword, CancellationToken ct = default)
    {
        var error = ValidatePassword(newPassword);
        if (error is not null)
            return OperationResult.Fail(error);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return OperationResult.Fail("Không tìm thấy tài khoản.");

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }

    public async Task<OperationResult> ChangeOwnPasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !PasswordHasher.Verify(currentPassword, user.PasswordHash))
            return OperationResult.Fail("Mật khẩu hiện tại không đúng.");
        return await SetPasswordAsync(userId, newPassword, ct);
    }

    public async Task<OperationResult> SetActiveAsync(int userId, bool active, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return OperationResult.Fail("Không tìm thấy tài khoản.");
        if (!active && user.Role == UserRole.Admin
            && !await db.Users.AnyAsync(u => u.Id != userId && u.Role == UserRole.Admin && u.IsActive, ct))
            return OperationResult.Fail("Phải còn ít nhất một quản trị viên đang hoạt động.");

        user.IsActive = active;
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }

    public Task<AppUser?> GetAsync(int userId, CancellationToken ct = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

    /// <summary>Starts setting up the authenticator app: a new secret that only counts once a code from it is confirmed.</summary>
    public async Task<OperationResult<string>> BeginTotpSetupAsync(int userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return OperationResult<string>.Fail("Không tìm thấy tài khoản.");
        if (user.TotpEnabled)
            return OperationResult<string>.Fail("Tài khoản này đã bật xác thực 2 lớp.");

        user.TotpSecret = Totp.GenerateSecret();
        await db.SaveChangesAsync(ct);
        return OperationResult<string>.Success(user.TotpSecret);
    }

    /// <summary>Turns two-step sign-in on once the first code from the app is typed correctly; returns the one-time recovery codes.</summary>
    public async Task<OperationResult<IReadOnlyList<string>>> ConfirmTotpAsync(int userId, string code, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.TotpSecret is null || user.TotpEnabled)
            return OperationResult<IReadOnlyList<string>>.Fail("Chưa có mã để xác nhận. Hãy bắt đầu cài đặt lại.");
        if (!Totp.Verify(user.TotpSecret, code, now ?? DateTimeOffset.UtcNow, user.TotpLastStep, out var step))
            return OperationResult<IReadOnlyList<string>>.Fail("Mã chưa đúng. Nhập mã 6 số đang hiện trong ứng dụng.");

        var recovery = Enumerable.Range(0, 8).Select(_ => NewRecoveryCode()).ToList();
        user.TotpEnabled = true;
        user.TotpLastStep = step;
        user.RecoveryCodeHashes = string.Join(',', recovery.Select(HashRecoveryCode));
        await db.SaveChangesAsync(ct);
        return OperationResult<IReadOnlyList<string>>.Success(recovery);
    }

    /// <summary>Second step of signing in: a code from the app, or one of the recovery codes (each works once).</summary>
    public async Task<bool> VerifySecondFactorAsync(int userId, string? code, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive || !user.TotpEnabled || user.TotpSecret is null)
            return false;

        if (Totp.Verify(user.TotpSecret, code, now ?? DateTimeOffset.UtcNow, user.TotpLastStep, out var step))
        {
            user.TotpLastStep = step;
            await db.SaveChangesAsync(ct);
            return true;
        }

        var hash = HashRecoveryCode(code ?? "");
        var hashes = (user.RecoveryCodeHashes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (hashes.Remove(hash))
        {
            user.RecoveryCodeHashes = string.Join(',', hashes);
            await db.SaveChangesAsync(ct);
            return true;
        }

        return false;
    }

    /// <summary>Turns two-step sign-in off. The caller must already have checked who is asking (own password, or an admin).</summary>
    public async Task<OperationResult> ResetTotpAsync(int userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return OperationResult.Fail("Không tìm thấy tài khoản.");

        user.TotpEnabled = false;
        user.TotpSecret = null;
        user.TotpLastStep = 0;
        user.RecoveryCodeHashes = null;
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }

    public async Task<bool> CheckPasswordAsync(int userId, string password, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is not null && PasswordHasher.Verify(password, user.PasswordHash);
    }

    private static string NewRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = RandomNumberGenerator.GetItems<char>(alphabet, 10);
        return new string(chars).Insert(5, "-");
    }

    private static string HashRecoveryCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant())));
    public static string? ValidateUsername(string username) =>
        username.Length is < 3 or > 40 || !username.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
            ? "Tên đăng nhập dài 3–40 ký tự, chỉ gồm chữ, số, dấu chấm, gạch dưới, gạch ngang."
            : null;

    public static string? ValidatePassword(string password) =>
        password.Length < PasswordHasher.MinimumLength
            ? $"Mật khẩu tối thiểu {PasswordHasher.MinimumLength} ký tự."
            : null;

    private static readonly string DummyHash = PasswordHasher.Hash("dummy-password-for-timing");
}
