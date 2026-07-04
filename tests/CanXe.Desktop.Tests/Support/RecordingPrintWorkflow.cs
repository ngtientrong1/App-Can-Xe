using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Desktop.Tests.Support;

public sealed class RecordingPrintWorkflow : IWeighTicketPrintWorkflow
{
    public int PrintInvocationCount { get; private set; }
    public int LastTicketId { get; private set; }
    public PrintCommandSource LastSource { get; private set; }
    public WeighTicketPrintResult NextResult { get; set; } = WeighTicketPrintResult.Succeeded(1, "TestPrinter");
    public WeighTicketPrintModel? NextModel { get; set; }

    public Task<WeighTicketPrintModel?> BuildPrintModelAsync(int ticketId, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextModel);

    public bool FailModelBuild { get; set; }

    public Task<WeighTicketPrintResult> PrintTicketAsync(int ticketId, PrintCommandSource source, CancellationToken cancellationToken = default)
    {
        PrintInvocationCount++;
        LastTicketId = ticketId;
        LastSource = source;
        if (FailModelBuild)
            return Task.FromResult(WeighTicketPrintResult.Failed("Không thể dựng nội dung phiếu để in."));
        return Task.FromResult(NextResult);
    }

    public Task PreviewTicketAsync(int ticketId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
