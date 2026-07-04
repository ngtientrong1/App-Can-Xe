using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class WeighTicketPrintSubmissionService(
    IWeighTicketDocumentFactory documentFactory,
    IPrinterCapabilityService printerCapability,
    IPrintJobHistoryRepository historyRepository,
    PrintCommandLogger commandLogger) : IWeighTicketPrintSubmissionService
{
    private readonly WeighTicketPrintRenderingLogger _logger = new();
    private readonly PrintScalingLogger _scalingLogger = new();
    private readonly WeighTicketRasterPrintRenderer _rasterRenderer = new();

    public async Task<WeighTicketPrintResult> PrintModelAsync(
        WeighTicketPrintModel model,
        PrintCommandSource source,
        CancellationToken cancellationToken = default)
    {
        commandLogger.Log($"print-model-started source={source} ticketId={model.TicketId} previewDependency=false");

        var printerName = model.PrintSettings.PreferredPrinterName;
        var validation = printerCapability.ValidatePrinter(printerName);
        if (!validation.Success || validation.Printer is null)
        {
            commandLogger.LogMilestone("PRINT_FAILED");
            return WeighTicketPrintResult.Failed(validation.ErrorMessage ?? "Không tìm thấy máy in khả dụng.");
        }

        commandLogger.LogMilestone($"PRINTER_RESOLVED name={validation.Printer.Name}");

        var layoutMode = ProductionPrintLayoutPolicy.ResolveProductionLayoutMode(model.PrintSettings);
        var paperSize = "A4";
        commandLogger.Log(
            $"LogicalDocument=A4TwoUp RequestedMedia=A4 RequestedOrientation=Portrait " +
            $"CopyCountOnPage=2 A5SingleTicketUsed=false RotationDegrees=0");
        var copies = Math.Max(1, model.PrintSettings.DefaultCopies);
        var historyId = await historyRepository.AddAsync(new PrintJobHistoryDto
        {
            TicketId = model.TicketId,
            TicketNumber = model.DisplayNumber,
            PrinterName = validation.Printer.Name,
            PaperSize = paperSize,
            Copies = copies,
            RequestedAt = DateTimeOffset.UtcNow,
            Status = "Prepared",
            IsReprint = model.IsReprint
        }, cancellationToken).ConfigureAwait(false);

        try
        {
            var printResult = await RunOnUiThreadAsync(() =>
                SubmitOnUiThread(model, validation.Printer.Name, copies, historyId, source)).ConfigureAwait(true);

            if (printResult.Success)
                await historyRepository.UpdateStatusAsync(historyId, "Submitted", null, cancellationToken).ConfigureAwait(false);
            else
            {
                commandLogger.LogMilestone("PRINT_FAILED");
                await historyRepository.UpdateStatusAsync(historyId, "Failed", printResult.ErrorMessage, cancellationToken).ConfigureAwait(false);
            }

            return printResult;
        }
        catch (Exception ex)
        {
            commandLogger.LogException("PRINT_FAILED", ex);
            await historyRepository.UpdateStatusAsync(historyId, "Failed", ex.Message, cancellationToken).ConfigureAwait(false);
            return WeighTicketPrintResult.Failed(ex.Message);
        }
    }

    private WeighTicketPrintResult SubmitOnUiThread(
        WeighTicketPrintModel model,
        string printerName,
        int copies,
        long historyId,
        PrintCommandSource source)
    {
        var mode = model.PrintSettings.PrintRenderingMode;
        FixedDocument? document = null;
        PrintA5AdaptiveFitOutcome? adaptiveOutcome = null;
        PageImageableArea? imageable = null;
        double pageWidthDip = 0;
        double pageHeightDip = 0;
        int bitmapWidth = 0;
        int bitmapHeight = 0;

        try
        {
            commandLogger.LogMilestone("FRESH_DOCUMENT_CREATED");

            using var server = new LocalPrintServer();
            var queue = server.GetPrintQueue(printerName);
            var driverName = queue.FullName;
            _logger.LogJobStarted(model, printerName, driverName, mode);

            var ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket;
            ticket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
            ticket.PageOrientation = PageOrientation.Portrait;
            ticket.CopyCount = copies;
            if (ticket.Duplexing is not null)
                ticket.Duplexing = Duplexing.OneSided;

            PrintCapabilities capabilities;
            capabilities = queue.GetPrintCapabilities(ticket);
            imageable = capabilities.PageImageableArea;
            pageWidthDip = WeighTicketPrintLayout.PageWidthDip;
            pageHeightDip = WeighTicketPrintLayout.PageHeightDip;

            if (mode == PrintRenderingMode.RasterCompatibility)
            {
                document = _rasterRenderer.CreateRasterDocument(model, imageable, _logger, out _, out adaptiveOutcome);
                bitmapWidth = WeighTicketRasterPrintRenderer.A4RasterPixelWidth;
                bitmapHeight = WeighTicketRasterPrintRenderer.A4RasterPixelHeight;
                commandLogger.LogMilestone($"A4_RASTER_CREATED width={bitmapWidth} height={bitmapHeight}");
            }
            else
            {
                document = documentFactory.CreateDocument(model);
                var page = document.Pages[0].GetPageRoot(false) as FrameworkElement;
                if (page is not null)
                    WpfWeighTicketDocumentFactory.Materialize(page);
            }

            DocumentPaginator paginator = document.DocumentPaginator;
            double scaleFactor = 1.0;

            if (mode != PrintRenderingMode.RasterCompatibility &&
                imageable is not null)
            {
                paginator = new ImageableAreaDocumentPaginator(
                    document.DocumentPaginator,
                    imageable,
                    pageWidthDip,
                    pageHeightDip,
                    out scaleFactor);
            }

            var printDialog = new PrintDialog
            {
                PrintQueue = queue,
                PrintTicket = ticket
            };

            commandLogger.LogMilestone($"SPOOL_SUBMIT_STARTED source={source} printer={printerName}");
            _logger.LogPaginator(imageable, scaleFactor, "started");
            printDialog.PrintDocument(paginator, $"CanXe {model.DisplayNumber}");
            _logger.LogPaginator(imageable, scaleFactor, "completed");
            commandLogger.LogMilestone($"SPOOL_SUBMIT_COMPLETED source={source} printer={printerName}");

            WriteLegacyDiagnostics(printerName, ticket, capabilities, imageable, scaleFactor);
            return WeighTicketPrintResult.Succeeded(historyId, printerName);
        }
        catch (Exception ex)
        {
            commandLogger.LogException("PRINT_FAILED", ex);
            _logger.LogError(ex.Message);
            return WeighTicketPrintResult.Failed(ex.Message);
        }
        finally
        {
            _ = document;
        }
    }

    private static Task<WeighTicketPrintResult> RunOnUiThreadAsync(Func<WeighTicketPrintResult> action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("WPF dispatcher is not available.");
        if (dispatcher.CheckAccess())
            return Task.FromResult(action());
        return dispatcher.InvokeAsync(action).Task;
    }

    private static void WriteLegacyDiagnostics(
        string printerName,
        PrintTicket ticket,
        PrintCapabilities capabilities,
        PageImageableArea? imageable,
        double scale)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanXe", "Logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, "print-diagnostics.log");
            var lines = new List<string>
            {
                $"[{DateTimeOffset.Now:O}] Print job",
                $"Printer: {printerName}",
                $"Media: {ticket.PageMediaSize?.PageMediaSizeName}",
                $"Orientation: {ticket.PageOrientation}",
                $"Duplex: {ticket.Duplexing}",
                $"Page size DIP: {capabilities.OrientedPageMediaWidth:F2} x {capabilities.OrientedPageMediaHeight:F2}"
            };
            if (imageable is not null)
            {
                lines.Add($"Imageable origin: {imageable.OriginWidth:F2} x {imageable.OriginHeight:F2}");
                lines.Add($"Imageable extent: {imageable.ExtentWidth:F2} x {imageable.ExtentHeight:F2}");
                lines.Add($"Final scale factor: {scale:F4}");
            }
            File.AppendAllLines(path, lines);
        }
        catch
        {
            // diagnostics only
        }
    }
}
