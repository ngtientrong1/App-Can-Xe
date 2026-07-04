using System.Windows;
using System.Windows.Documents;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Windows;

namespace CanXe.Desktop.Services;

public sealed class WeighTicketPrintWorkflow(
    WeighTicketService weighTicketService,
    StationSettingsService stationSettings,
    PrintSettingsService printSettings,
    IWeighTicketPrintSubmissionService submissionService,
    IWeighTicketDocumentFactory documentFactory,
    IPrintNotificationService notificationService,
    PrintCommandLogger commandLogger) : IWeighTicketPrintWorkflow
{
    public async Task<WeighTicketPrintModel?> BuildPrintModelAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        commandLogger.LogMilestone($"MODEL_BUILD_STARTED ticketId={ticketId} previewDependency=false");
        var detail = await weighTicketService.GetTicketDetailAsync(ticketId, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            commandLogger.Log($"model-build-failed ticketId={ticketId} reason=ticket-not-found");
            return null;
        }

        var station = await stationSettings.GetStationAsync(cancellationToken).ConfigureAwait(false);
        var settings = await printSettings.GetAsync(cancellationToken).ConfigureAwait(false);
        var model = WeighTicketPrintModelMapper.FromDetail(detail, station, settings, isReprint: detail.Id > 0);
        commandLogger.LogMilestone($"MODEL_BUILD_COMPLETED ticketId={ticketId} layoutMode={model.PrintSettings.PrintLayoutMode}");
        return model;
    }

    public async Task<WeighTicketPrintResult> PrintTicketAsync(
        int ticketId,
        PrintCommandSource source,
        CancellationToken cancellationToken = default)
    {
        commandLogger.Log($"print-started source={source} ticketId={ticketId} previewDependency=false");
        var model = await BuildPrintModelAsync(ticketId, cancellationToken).ConfigureAwait(true);
        if (model is null)
        {
            commandLogger.LogMilestone("PRINT_FAILED");
            var failed = WeighTicketPrintResult.Failed("Không thể dựng nội dung phiếu để in.");
            if (source == PrintCommandSource.MainWindow)
                notificationService.ShowPrintError(failed.ErrorMessage);
            return failed;
        }

        return await PrintModelAsync(model, source, cancellationToken).ConfigureAwait(true);
    }

    public async Task<WeighTicketPrintResult> PrintModelAsync(
        WeighTicketPrintModel model,
        PrintCommandSource source,
        CancellationToken cancellationToken = default)
    {
        var result = await submissionService.PrintModelAsync(model, source, cancellationToken).ConfigureAwait(true);
        if (result.Success)
        {
            commandLogger.LogMilestone("SUCCESS_DIALOG_SHOWN");
            await notificationService.ShowSuccessToastAsync(result.PrinterName, cancellationToken).ConfigureAwait(true);
            commandLogger.Log($"print-completed source={source} ticketId={model.TicketId} printer={result.PrinterName}");
        }
        else
        {
            commandLogger.LogMilestone("ERROR_DIALOG_SHOWN");
            notificationService.ShowPrintError(result.ErrorMessage);
            commandLogger.Log($"print-failed source={source} ticketId={model.TicketId} error={result.ErrorMessage}");
        }

        return result;
    }

    public async Task PreviewTicketAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        commandLogger.Log($"preview-started source=MainWindow ticketId={ticketId}");
        var model = await BuildPrintModelAsync(ticketId, cancellationToken).ConfigureAwait(true);
        if (model is null)
            throw new InvalidOperationException("Không thể dựng nội dung phiếu để xem.");

        var document = await CreateFreshDocumentAsync(model).ConfigureAwait(true);
        var previewInfo = PrintPreviewScaleResolver.Resolve(model.PrintSettings.PrintLayoutMode);
        var window = new WeighTicketPrintPreviewWindow(document, async () =>
        {
            await PrintTicketAsync(ticketId, PrintCommandSource.PreviewWindow, cancellationToken).ConfigureAwait(true);
        }, previewInfo)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
        commandLogger.Log($"preview-closed ticketId={ticketId}");
    }

    private Task<FixedDocument> CreateFreshDocumentAsync(WeighTicketPrintModel model)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
            return Task.FromResult(CreateFreshDocument(model));
        return dispatcher.InvokeAsync(() => CreateFreshDocument(model)).Task;
    }

    private FixedDocument CreateFreshDocument(WeighTicketPrintModel model)
    {
        var document = documentFactory.CreateDocument(model);
        var page = document.Pages[0].GetPageRoot(false) as FrameworkElement;
        if (page is not null)
            WpfWeighTicketDocumentFactory.Materialize(page);
        return document;
    }
}
