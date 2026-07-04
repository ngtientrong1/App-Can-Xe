using System.Windows.Media;

namespace CanXe.Desktop.Services;

internal static class WeighTicketPrintResources
{
    public static readonly SolidColorBrush PageBackground = Brushes.White;
    public static readonly SolidColorBrush MainText = Brushes.Black;
    public static readonly SolidColorBrush CardBorder = Brushes.Black;
    public static readonly SolidColorBrush IconTileBackground = Brushes.Black;
    public static readonly SolidColorBrush IconForeground = Brushes.White;
    public static readonly SolidColorBrush DividerBrush = new(Color.FromRgb(0x70, 0x70, 0x70));
}
