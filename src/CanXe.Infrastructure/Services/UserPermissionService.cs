using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Models;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Services;

public sealed class UserPermissionService(IAdminAuthorizationService adminAuthorization) : IUserPermissionService
{
    private const string RequireAdminMessage = "Cần mở khóa Admin để thực hiện thao tác này.";

    public StationUserRole CurrentRole =>
        adminAuthorization.IsAdminUnlocked ? StationUserRole.Admin : StationUserRole.Operator;

    public bool HasPermission(AdminPermission permission) =>
        adminAuthorization.IsAdminUnlocked;

    public PermissionCheckResult EnsurePermission(AdminPermission permission, string action)
    {
        if (HasPermission(permission))
        {
            adminAuthorization.TouchAdminActivity();
            return PermissionCheckResult.Allow(action);
        }

        AdminAuditLogger.Write(
            "PERMISSION_DENIED",
            "DENIED",
            CurrentRole.ToString(),
            note: $"{action}:{permission}");

        return PermissionCheckResult.Deny(action, RequireAdminMessage);
    }
}
