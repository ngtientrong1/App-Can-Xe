using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;

namespace CanXe.Infrastructure.Services;

public sealed class DeveloperAuthorizationService(AppSettings settings) : IDeveloperAuthorizationService
{
    public bool CanDeleteTickets =>
        settings.DeveloperMode && settings.DeveloperTicketEditEnabled;
}
