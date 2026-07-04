using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public sealed class PrintSettingsService(IPrintSettingsRepository repository)
{
    public async Task<PrintSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await repository.GetAsync(cancellationToken).ConfigureAwait(false) ?? new PrintSettingsDto();
        return ProductionPrintLayoutPolicy.NormalizeForProduction(settings);
    }

    public Task SaveAsync(PrintSettingsDto settings, CancellationToken cancellationToken = default) =>
        repository.SaveAsync(settings, cancellationToken);
}
