using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CanXe.Desktop.Windows;

public enum PrintPreviewZoomMode
{
    FitWidth,
    FitPage,
    ActualSize
}

public sealed class WeighTicketPrintPreviewWindow : Window
{
    private readonly DocumentViewer _viewer = new();
    private readonly TextBlock _modeLabel = new();
    private readonly PrintPreviewInfo? _previewInfo;
    private PrintPreviewZoomMode _zoomMode = PrintPreviewZoomMode.FitWidth;

    public WeighTicketPrintPreviewWindow(FixedDocument document, Func<Task> printAsync, PrintPreviewInfo? previewInfo = null)
    {
        _previewInfo = previewInfo;
        Title = previewInfo is null
            ? "Xem trước phiếu cân"
            : "Xem trước phiếu cân — A5 ngang";
        Width = 920;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));

        _viewer.Document = document;

        _modeLabel.Text = previewInfo?.ModeLabel ?? "A5 ngang — 1 phiếu";
        _modeLabel.FontSize = 13;
        _modeLabel.Margin = new Thickness(8, 8, 8, 0);
        _modeLabel.Foreground = Brushes.Black;

        var fitWidth = new Button { Content = "FIT WIDTH", MinWidth = 100, Margin = new Thickness(4) };
        fitWidth.Click += (_, _) => ApplyZoom(PrintPreviewZoomMode.FitWidth);

        var fitPage = new Button { Content = "FIT PAGE", MinWidth = 100, Margin = new Thickness(4) };
        fitPage.Click += (_, _) => ApplyZoom(PrintPreviewZoomMode.FitPage);

        var actual = new Button { Content = "100%", MinWidth = 80, Margin = new Thickness(4) };
        actual.Click += (_, _) => ApplyZoom(PrintPreviewZoomMode.ActualSize);

        var printButton = new Button { Content = "IN", MinWidth = 100, Margin = new Thickness(4) };
        printButton.Click += async (_, _) => await printAsync();

        var closeButton = new Button { Content = "ĐÓNG", MinWidth = 100, Margin = new Thickness(4) };
        closeButton.Click += (_, _) => Close();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(4, 4, 4, 0)
        };
        toolbar.Children.Add(_modeLabel);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0)
        };
        buttons.Children.Add(fitWidth);
        buttons.Children.Add(fitPage);
        buttons.Children.Add(actual);
        buttons.Children.Add(printButton);
        buttons.Children.Add(closeButton);

        var header = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        header.Children.Add(buttons);
        header.Children.Add(toolbar);

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(_viewer);
        Content = root;

        Loaded += (_, _) => ApplyZoom(PrintPreviewZoomMode.FitWidth);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void ApplyZoom(PrintPreviewZoomMode mode)
    {
        _zoomMode = mode;
        var scaleSuffix = _previewInfo is null
            ? string.Empty
            : $" — fit { _previewInfo.FinalScalePercent:F1}%";

        switch (mode)
        {
            case PrintPreviewZoomMode.FitPage:
                _viewer.FitToMaxPagesAcross(1);
                _modeLabel.Text = $"{_previewInfo?.ModeLabel ?? "A5 ngang"} — FIT PAGE{scaleSuffix}";
                break;
            case PrintPreviewZoomMode.ActualSize:
                _viewer.FitToMaxPagesAcross(0);
                _modeLabel.Text = $"{_previewInfo?.ModeLabel ?? "A5 ngang"} — 100%{scaleSuffix}";
                break;
            default:
                _viewer.FitToWidth();
                _modeLabel.Text = $"{_previewInfo?.ModeLabel ?? "A5 ngang"} — FIT WIDTH{scaleSuffix}";
                break;
        }
    }
}
