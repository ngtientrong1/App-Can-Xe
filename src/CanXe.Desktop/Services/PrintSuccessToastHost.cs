using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace CanXe.Desktop.Services;

public sealed class PrintSuccessToastHost
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(2500);

    private readonly object _gate = new();
    private Popup? _popup;
    private CancellationTokenSource? _dismissCts;
    private long _activeJobId;

    public async Task ShowAsync(
        Window? owner,
        long jobId,
        string? printerName,
        TimeSpan duration,
        PrintCommandLogger? commandLogger,
        CancellationToken cancellationToken = default) =>
        await ShowMessageAsync(
            owner,
            jobId,
            "ĐÃ GỬI LỆNH IN THÀNH CÔNG",
            string.IsNullOrWhiteSpace(printerName)
                ? "Đã gửi phiếu cân đến máy in."
                : $"Đã gửi phiếu cân đến {printerName}.",
            duration,
            commandLogger,
            "SUCCESS_TOAST_SHOWN",
            "SUCCESS_TOAST_AUTO_DISMISSED",
            cancellationToken).ConfigureAwait(true);

    public async Task ShowMessageAsync(
        Window? owner,
        long jobId,
        string title,
        string detail,
        TimeSpan duration,
        PrintCommandLogger? commandLogger,
        string shownMilestone,
        string dismissedMilestone,
        CancellationToken cancellationToken = default)
    {
        if (owner is null)
            return;

        CancellationToken dismissToken;
        lock (_gate)
        {
            _dismissCts?.Cancel();
            _dismissCts?.Dispose();
            _dismissCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            dismissToken = _dismissCts.Token;
            _activeJobId = jobId;
            DismissUi(owner);
        }

        await owner.Dispatcher.InvokeAsync(() =>
        {
            var border = CreateToastBorder(title, detail);
            border.MouseLeftButtonUp += (_, _) => DismissUi(owner);

            // Anchor to window content (not title chrome) so toast is not clipped at top edge.
            var placementTarget = owner.Content as FrameworkElement ?? owner;
            var popup = new Popup
            {
                AllowsTransparency = true,
                Placement = PlacementMode.Relative,
                PlacementTarget = placementTarget,
                HorizontalOffset = Math.Max(24, placementTarget.ActualWidth - 360),
                VerticalOffset = 24,
                StaysOpen = true,
                Focusable = false,
                IsHitTestVisible = true,
                PopupAnimation = PopupAnimation.Fade,
                Child = border
            };
            popup.Opened += (_, _) =>
            {
                commandLogger?.LogMilestone(shownMilestone);
                popup.HorizontalOffset = Math.Max(24, placementTarget.ActualWidth - 360);
                popup.VerticalOffset = 24;
            };
            popup.IsOpen = true;
            _popup = popup;
        });

        try
        {
            await Task.Delay(duration, dismissToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            if (_activeJobId != jobId)
                return;
        }

        await owner.Dispatcher.InvokeAsync(() =>
        {
            DismissUi(owner);
            commandLogger?.LogMilestone(dismissedMilestone);
        });
    }

    public void DismissForShutdown()
    {
        lock (_gate)
        {
            _dismissCts?.Cancel();
            _dismissCts?.Dispose();
            _dismissCts = null;
            if (_popup is not null)
            {
                _popup.IsOpen = false;
                _popup = null;
            }
        }
    }

    private void DismissUi(Window owner)
    {
        _ = owner;
        if (_popup is not null)
        {
            _popup.IsOpen = false;
            _popup = null;
        }
    }

    private static Border CreateToastBorder(string title, string detail)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = TryThemeBrush("ToastTextBrush", Colors.White),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(detail))
        {
            panel.Children.Add(new TextBlock
            {
                Text = detail,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = TryThemeBrush("ToastDetailBrush", Color.FromRgb(220, 228, 240)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return new Border
        {
            Background = TryThemeBrush("ToastBackgroundBrush", Color.FromArgb(235, 38, 50, 56)),
            BorderBrush = TryThemeBrush("ToastBorderBrush", Color.FromRgb(76, 175, 80)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            MaxWidth = 340,
            MinWidth = 220,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.35,
                Color = Colors.Black
            },
            Child = panel
        };
    }

    private static Brush TryThemeBrush(string key, Color fallbackColor) =>
        System.Windows.Application.Current?.TryFindResource(key) as Brush
        ?? new SolidColorBrush(fallbackColor);
}
