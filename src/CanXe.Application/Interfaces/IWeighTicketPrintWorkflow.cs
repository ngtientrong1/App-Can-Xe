using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IWeighTicketPrintWorkflow
{
    Task<WeighTicketPrintModel?> BuildPrintModelAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<WeighTicketPrintResult> PrintTicketAsync(int ticketId, PrintCommandSource source, CancellationToken cancellationToken = default);
    Task PreviewTicketAsync(int ticketId, CancellationToken cancellationToken = default);
}
