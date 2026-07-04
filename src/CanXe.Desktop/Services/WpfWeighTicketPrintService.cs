using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Desktop.Services;

public sealed class WpfWeighTicketPrintService(
    IWeighTicketPrintSubmissionService submissionService,
    IPrintJobHistoryRepository historyRepository) : IWeighTicketPrintService
{
    public Task<WeighTicketPrintResult> PrintAsync(WeighTicketPrintRequest request, CancellationToken cancellationToken = default) =>
        submissionService.PrintModelAsync(request.Model, PrintCommandSource.MainWindow, cancellationToken);

    public Task<long> RecordPrintJobAsync(PrintJobHistoryDto job, CancellationToken cancellationToken = default) =>
        historyRepository.AddAsync(job, cancellationToken);

    public Task UpdatePrintJobAsync(long id, string status, string? errorMessage, CancellationToken cancellationToken = default) =>
        historyRepository.UpdateStatusAsync(id, status, errorMessage, cancellationToken);

    public Task<IReadOnlyList<PrintJobHistoryDto>> GetHistoryForTicketAsync(int ticketId, CancellationToken cancellationToken = default) =>
        historyRepository.GetForTicketAsync(ticketId, cancellationToken);
}
