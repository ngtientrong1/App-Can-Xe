using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IWeighTicketPrintSubmissionService
{
    Task<WeighTicketPrintResult> PrintModelAsync(
        WeighTicketPrintModel model,
        PrintCommandSource source,
        CancellationToken cancellationToken = default);
}
