using System.Windows;
using System.Windows.Media.Imaging;

namespace CanXe.Desktop.Views.Reports;

public partial class ReportPreviewWindow : Window
{
    public ReportPreviewWindow(IReadOnlyList<BitmapSource> pages, int ticketCount)
    {
        InitializeComponent();
        PagesControl.ItemsSource = pages;
        SummaryText.Text = $"Báo cáo {ticketCount:N0} phiếu  -  {pages.Count} trang";
    }

    private void PrintButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
