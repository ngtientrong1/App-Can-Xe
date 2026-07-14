using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CanXe.Desktop.Views.Reports;

public partial class ReportView
{
    public ReportView() => InitializeComponent();

    private void ReportDataGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
            return;

        var scrollViewer = FindScrollViewer(dataGrid);
        if (scrollViewer is null)
            return;

        var newOffset = scrollViewer.VerticalOffset - e.Delta;
        if (newOffset < 0)
            newOffset = 0;
        else if (newOffset > scrollViewer.ScrollableHeight)
            newOffset = scrollViewer.ScrollableHeight;

        scrollViewer.ScrollToVerticalOffset(newOffset);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer scrollViewer)
                return scrollViewer;

            var nested = FindScrollViewer(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
