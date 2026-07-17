using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Desktop.Tests.Support;

public sealed class RecordingPrintNotificationService : IPrintNotificationService
{
    public int SuccessCount { get; private set; }
    public int ErrorCount { get; private set; }
    public string? LastSuccessPrinter { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public PrintDirtyChoice NextDirtyChoice { get; set; } = PrintDirtyChoice.PrintSaved;

    public Task ShowSuccessToastAsync(string? printerName, CancellationToken cancellationToken = default)
    {
        SuccessCount++;
        LastSuccessPrinter = printerName;
        return Task.CompletedTask;
    }

    public void ShowPrintSuccess(string? printerName) =>
        _ = ShowSuccessToastAsync(printerName);

    public Task ShowDeleteSuccessToastAsync(string? displayNumber, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public string? LastCatalogDeleteToast { get; private set; }

    public Task ShowCatalogDeleteSuccessToastAsync(string? message = null, CancellationToken cancellationToken = default)
    {
        LastCatalogDeleteToast = string.IsNullOrWhiteSpace(message) ? "ĐÃ XÓA KHỎI DANH MỤC" : message.Trim();
        return Task.CompletedTask;
    }

    public Task ShowExportSuccessToastAsync(string? filePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void ShowPrintError(string? detailMessage)
    {
        ErrorCount++;
        LastErrorMessage = detailMessage;
    }

    public PrintDirtyChoice PromptDirtyPrintChoice() => NextDirtyChoice;
}
