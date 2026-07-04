using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IPrintNotificationService
{
    void ShowPrintSuccess(string? printerName);
    Task ShowSuccessToastAsync(string? printerName, CancellationToken cancellationToken = default);
    Task ShowDeleteSuccessToastAsync(string? displayNumber, CancellationToken cancellationToken = default);
    void ShowPrintError(string? detailMessage);
    PrintDirtyChoice PromptDirtyPrintChoice();
}
