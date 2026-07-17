using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Services;

public sealed class AdminAuthorizationService : IAdminAuthorizationService, IDisposable
{
    private readonly AppSettings _settings;
    private readonly object _gate = new();
    private readonly System.Threading.Timer _idleTimer;
    private bool _isUnlocked;
    private DateTimeOffset _lastActivityUtc = DateTimeOffset.MinValue;

    public AdminAuthorizationService(AppSettings settings, TimeSpan? idleTimeout = null)
    {
        _settings = settings;
        IdleTimeout = idleTimeout ?? TimeSpan.FromMinutes(10);
        // Keep polling short so timeout locks apply without affecting COM/UI work.
        var poll = TimeSpan.FromMilliseconds(Math.Clamp(IdleTimeout.TotalMilliseconds / 4.0, 50, 30_000));
        _idleTimer = new System.Threading.Timer(OnIdleTick, null, poll, poll);
    }

    /// <summary>Runs idle timeout evaluation (also used by tests with short IdleTimeout).</summary>
    public void CheckIdleTimeoutNow() => OnIdleTick(null);

    public bool IsAdminUnlocked
    {
        get
        {
            lock (_gate)
                return _isUnlocked;
        }
    }

    public TimeSpan IdleTimeout { get; }

    public event EventHandler? AdminSessionChanged;

    public AdminUnlockResult TryUnlock(string? password)
    {
        var expected = string.IsNullOrWhiteSpace(_settings.AdminUnlockCode)
            ? "admin123"
            : _settings.AdminUnlockCode.Trim();

        if (string.IsNullOrEmpty(password) ||
            !string.Equals(password, expected, StringComparison.Ordinal))
        {
            AdminAuditLogger.Write(
                "ADMIN_UNLOCK",
                "FAIL",
                "Operator",
                note: "wrong-code");
            return AdminUnlockResult.Fail("Mã Admin không đúng.");
        }

        lock (_gate)
        {
            _isUnlocked = true;
            _lastActivityUtc = DateTimeOffset.UtcNow;
        }

        AdminAuditLogger.Write("ADMIN_UNLOCK", "SUCCESS", "Admin");
        RaiseChanged();
        return AdminUnlockResult.Ok();
    }

    public void Lock(string reason = "manual")
    {
        var wasUnlocked = false;
        lock (_gate)
        {
            wasUnlocked = _isUnlocked;
            _isUnlocked = false;
            _lastActivityUtc = DateTimeOffset.MinValue;
        }

        if (wasUnlocked)
        {
            AdminAuditLogger.Write(
                "ADMIN_LOCK",
                "SUCCESS",
                "Admin",
                note: reason);
            RaiseChanged();
        }
    }

    public void TouchAdminActivity()
    {
        lock (_gate)
        {
            if (!_isUnlocked)
                return;
            _lastActivityUtc = DateTimeOffset.UtcNow;
        }
    }

    private void OnIdleTick(object? state)
    {
        bool shouldLock;
        lock (_gate)
        {
            shouldLock = _isUnlocked &&
                         _lastActivityUtc != DateTimeOffset.MinValue &&
                         DateTimeOffset.UtcNow - _lastActivityUtc >= IdleTimeout;
        }

        if (shouldLock)
            Lock("timeout");
    }

    private void RaiseChanged() => AdminSessionChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _idleTimer.Dispose();
}
