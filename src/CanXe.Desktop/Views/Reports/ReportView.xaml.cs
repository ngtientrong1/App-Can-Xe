using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CanXe.Application.Models;
using CanXe.Desktop.Controls;
using CanXe.Desktop.ViewModels;

namespace CanXe.Desktop.Views.Reports;

public partial class ReportView
{
    public ReportView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            WireFilterBox(CustomerBox, AutocompleteField.Customer);
            WireFilterBox(VehicleBox, AutocompleteField.Vehicle);
            WireFilterBox(CargoBox, AutocompleteField.CargoType);
        };
    }

    // Picking an entry from the dropdown filters straight away; typed text still needs LỌC.
    private void WireFilterBox(AutoCompleteTextBox box, AutocompleteField field)
    {
        if (box.Tag is true)
            return;

        box.Tag = true;

        box.ItemCommitted += (_, _) =>
        {
            if (DataContext is ReportViewModel vm &&
                box.SelectedItem is AutocompleteSuggestionItem { IsNewEntryOption: false } &&
                vm.ApplyFilterCommand.CanExecute(null))
            {
                vm.ApplyFilterCommand.Execute(null);
            }
        };

        box.HistoryRequested += (_, _) =>
        {
            if (DataContext is ReportViewModel vm)
                _ = vm.ShowHistoryAsync(field, box);
        };
    }

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
