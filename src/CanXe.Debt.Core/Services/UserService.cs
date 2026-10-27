using System.Collections.Concurrent;
using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

/// <summary>Slows down password guessing: five wrong tries lock the username for ten minutes.</summary>
public sealed class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (int Failures, DateTimeOffset LockedUntil)> _state = new();
    private readonly Func<DateTimeOffset> _now;

    public LoginThrottle() : this(() => DateTimeOffset.UtcNow) { }

    public LoginThrottle(Func<DateTimeOffset> now) => _now = now;

    public bool IsLocked(string username) =>
        _state.TryGetValue(Key(username), out var s) && s.LockedUntil > _now();

    public void RegisterFailure(string username)
    {
        _state.AddOrUpdate(
            Key(username),
            _ => (1, DateTimeOffset.MinValue),
            (_, s) =>
            {
                if (s.LockedUntil > _now())
                    return s;
                var failures = s.Failures + 1;
                return failures >= MaxFailures ? (0, _now() + LockFor) : (failures, DateTimeOffset.MinValue);
            });
    }

    public void Reset(string username) => _state.TryRemove(Key(username), out _);

    private static string Key(string username) => username.Trim().ToLowerInvariant();
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
