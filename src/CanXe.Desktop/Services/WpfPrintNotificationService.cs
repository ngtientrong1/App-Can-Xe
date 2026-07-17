using System.Windows;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Windows;

namespace CanXe.Desktop.Services;

public sealed class WpfPrintNotificationService(PrintCommandLogger commandLogger) : IPrintNotificationService
{
  public static readonly TimeSpan DefaultSuccessToastDuration = PrintSuccessToastHost.DefaultDuration;

  private readonly PrintSuccessToastHost _toastHost = new();
  private long _toastJobSequence;

  public void ShowPrintSuccess(string? printerName) =>
    _ = ShowSuccessToastAsync(printerName);

  public Task ShowSuccessToastAsync(string? printerName, CancellationToken cancellationToken = default)
  {
    var jobId = Interlocked.Increment(ref _toastJobSequence);
    var owner = ResolveOwnerWindow();
    return _toastHost.ShowAsync(
      owner,
      jobId,
      printerName,
      DefaultSuccessToastDuration,
      commandLogger,
      cancellationToken);
  }

  public Task ShowDeleteSuccessToastAsync(string? displayNumber, CancellationToken cancellationToken = default)
  {
    var jobId = Interlocked.Increment(ref _toastJobSequence);
    var owner = ResolveOwnerWindow();
    var title = string.IsNullOrWhiteSpace(displayNumber)
      ? "ĐÃ XÓA PHIẾU"
      : $"ĐÃ XÓA PHIẾU {displayNumber}";
    return _toastHost.ShowMessageAsync(
      owner,
      jobId,
      title,
      string.Empty,
      DefaultSuccessToastDuration,
      commandLogger,
      "DELETE_TOAST_SHOWN",
      "DELETE_TOAST_AUTO_DISMISSED",
      cancellationToken);
  }

  public Task ShowCatalogDeleteSuccessToastAsync(string? message = null, CancellationToken cancellationToken = default)
  {
    var jobId = Interlocked.Increment(ref _toastJobSequence);
    var owner = ResolveOwnerWindow();
    var title = string.IsNullOrWhiteSpace(message) ? "ĐÃ XÓA KHỎI DANH MỤC" : message.Trim();
    return _toastHost.ShowMessageAsync(
      owner,
      jobId,
      title,
      string.Empty,
      DefaultSuccessToastDuration,
      commandLogger,
      "CATALOG_DELETE_TOAST_SHOWN",
      "CATALOG_DELETE_TOAST_AUTO_DISMISSED",
      cancellationToken);
  }

  public Task ShowExportSuccessToastAsync(string? filePath, CancellationToken cancellationToken = default)
  {
    var jobId = Interlocked.Increment(ref _toastJobSequence);
    var owner = ResolveOwnerWindow();
    var detail = string.IsNullOrWhiteSpace(filePath)
      ? string.Empty
      : filePath;
    return _toastHost.ShowMessageAsync(
      owner,
      jobId,
      "ĐÃ XUẤT FILE EXCEL",
      detail,
      DefaultSuccessToastDuration,
      commandLogger,
      "EXPORT_TOAST_SHOWN",
      "EXPORT_TOAST_AUTO_DISMISSED",
      cancellationToken);
  }

  public void ShowPrintError(string? detailMessage)
  {
    RunOnUiThread(() =>
    {
      string body;
      if (!string.IsNullOrWhiteSpace(detailMessage) &&
          detailMessage.Contains("chưa thể thu gọn", StringComparison.Ordinal))
      {
        body = detailMessage;
      }
      else
      {
        body = string.IsNullOrWhiteSpace(detailMessage)
          ? "Không thể gửi phiếu cân đến máy in.\nVui lòng kiểm tra máy in và thử lại."
          : $"Không thể gửi phiếu cân đến máy in.\nVui lòng kiểm tra máy in và thử lại.\n\n{detailMessage}";
      }

      MessageBox.Show(
        ResolveOwnerWindow(),
        body,
        "KHÔNG THỂ IN PHIẾU",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
    });
  }

  public PrintDirtyChoice PromptDirtyPrintChoice() =>
    RunOnUiThread(() =>
    {
      var dialog = new PrintDirtyChoiceDialog
      {
        Owner = ResolveOwnerWindow()
      };
      return dialog.ShowDialog() == true ? dialog.Choice : PrintDirtyChoice.Cancel;
    });

  public void DismissActiveToasts() => _toastHost.DismissForShutdown();

  private static Window? ResolveOwnerWindow()
  {
    var app = System.Windows.Application.Current;
    if (app is null)
      return null;

    return app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
      ?? app.MainWindow;
  }

  private static void RunOnUiThread(Action action)
  {
    var dispatcher = System.Windows.Application.Current?.Dispatcher
      ?? throw new InvalidOperationException("WPF dispatcher is not available.");
    if (dispatcher.CheckAccess())
      action();
    else
      dispatcher.Invoke(action);
  }

  private static T RunOnUiThread<T>(Func<T> func)
  {
    var dispatcher = System.Windows.Application.Current?.Dispatcher
      ?? throw new InvalidOperationException("WPF dispatcher is not available.");
    return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func);
  }
}
