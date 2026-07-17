namespace CanXe.Application.Interfaces;

public interface IAdminAuthorizationService
{
    bool IsAdminUnlocked { get; }

    TimeSpan IdleTimeout { get; }

    event EventHandler? AdminSessionChanged;

    AdminUnlockResult TryUnlock(string? password);

    void Lock(string reason = "manual");

    void TouchAdminActivity();
}

public sealed class AdminUnlockResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static AdminUnlockResult Ok() => new() { Success = true };

    public static AdminUnlockResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
