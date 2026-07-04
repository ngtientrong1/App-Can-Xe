using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IWeighTicketDocumentBuilder
{
    IDocumentPageSource BuildA4TwoUpDocument(WeighTicketPrintModel model);
}

public interface IDocumentPageSource
{
    double PageWidthDip { get; }
    double PageHeightDip { get; }
    int PageCount { get; }
    object GetPage(int pageIndex);
}

public sealed class PrinterInfo
{
    public required string Name { get; init; }
    public bool IsDefault { get; init; }
    public bool SupportsA4 { get; init; }
    public bool IsOnline { get; init; }
}

public sealed class PrinterValidationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public PrinterInfo? Printer { get; init; }

    public static PrinterValidationResult Ok(PrinterInfo printer) =>
        new() { Success = true, Printer = printer };

    public static PrinterValidationResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public interface IPrinterCapabilityService
{
    IReadOnlyList<PrinterInfo> GetInstalledPrinters();
    PrinterInfo? GetDefaultPrinter();
    PrinterValidationResult ValidatePrinter(string? preferredPrinterName);
}

public sealed class WeighTicketPrintRequest
{
    public required WeighTicketPrintModel Model { get; init; }
    public string? PrinterName { get; init; }
    public int Copies { get; init; } = 1;
}

public sealed class WeighTicketPrintResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public long? PrintJobHistoryId { get; init; }
    public string? PrinterName { get; init; }

    public static WeighTicketPrintResult Succeeded(long historyId, string printerName) =>
        new() { Success = true, PrintJobHistoryId = historyId, PrinterName = printerName };

    public static WeighTicketPrintResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public interface IWeighTicketPrintService
{
    Task<WeighTicketPrintResult> PrintAsync(WeighTicketPrintRequest request, CancellationToken cancellationToken = default);
    Task<long> RecordPrintJobAsync(PrintJobHistoryDto job, CancellationToken cancellationToken = default);
    Task UpdatePrintJobAsync(long id, string status, string? errorMessage, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrintJobHistoryDto>> GetHistoryForTicketAsync(int ticketId, CancellationToken cancellationToken = default);
}
