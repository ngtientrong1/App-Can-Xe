using CanXe.Application.Models;
using CanXe.Domain.Models;

namespace CanXe.Application.Interfaces;

public interface IUserPermissionService
{
    StationUserRole CurrentRole { get; }

    bool HasPermission(AdminPermission permission);

    PermissionCheckResult EnsurePermission(AdminPermission permission, string action);
}

public sealed class PermissionCheckResult
{
    public bool Allowed { get; init; }
    public string? ErrorMessage { get; init; }
    public string Action { get; init; } = string.Empty;

    public static PermissionCheckResult Allow(string action) =>
        new() { Allowed = true, Action = action };

    public static PermissionCheckResult Deny(string action, string message) =>
        new()
        {
            Allowed = false,
            Action = action,
            ErrorMessage = message
        };
}
