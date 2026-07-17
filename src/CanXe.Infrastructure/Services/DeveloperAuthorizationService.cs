using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Infrastructure.Services;

/// <summary>
/// Legacy delete gate. Phase 6 prefers Admin unlock via <see cref="IUserPermissionService"/>.
/// AppSettings overload remains for older tests.
/// </summary>
public sealed class DeveloperAuthorizationService : IDeveloperAuthorizationService
{
    private readonly Func<bool> _canDeleteTickets;

    public DeveloperAuthorizationService(IUserPermissionService permissions)
        : this(() => permissions.HasPermission(AdminPermission.CanDeleteTicket))
    {
    }

    public DeveloperAuthorizationService(AppSettings settings)
        : this(() => settings.DeveloperMode && settings.DeveloperTicketEditEnabled)
    {
    }

    public DeveloperAuthorizationService(Func<bool> canDeleteTickets) =>
        _canDeleteTickets = canDeleteTickets;

    public bool CanDeleteTickets => _canDeleteTickets();
}
