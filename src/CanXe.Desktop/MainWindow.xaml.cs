using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CanXe.Application.Models;
using CanXe.Desktop.Controls;
using CanXe.Desktop.Services;
using CanXe.Desktop.ViewModels;

namespace CanXe.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(IUiFocusService focusService)
    {
        InitializeComponent();
        if (focusService is WpfUiFocusService wpfFocus)
            wpfFocus.RegisterWindow(this);

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Deactivated += OnWindowDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        WireAutocomplete(CustomerField, AutocompleteField.Customer);
        WireAutocomplete(VehicleField, AutocompleteField.Vehicle);
        WireAutocomplete(CargoField, AutocompleteField.CargoType);
        WireAutocomplete(NotesField, AutocompleteField.Notes);

        if (DataContext is MainViewModel vm)
            vm.UpdateWindowSize(ActualWidth, ActualHeight);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.UpdateWindowSize(e.NewSize.Width, e.NewSize.Height);
    }

    private void UnitPriceField_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.BeginUnitPriceEdit();
    }

    private void UnitPriceField_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.CommitUnitPriceEdit();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) =>
        AutoCompleteTextBox.CloseAllDropDowns();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
    }

    private void MainPrintButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not Button button)
            return;

        var command = button.Command;
        vm.RecordMainPrintButtonClick(new MainPrintButtonTelemetry
        {
            ButtonName = button.Name,
            IsEnabled = button.IsEnabled,
            IsHitTestVisible = button.IsHitTestVisible,
            DataContextType = button.DataContext?.GetType().Name ?? "null",
            CommandType = command?.GetType().Name ?? "null",
            CanExecute = command?.CanExecute(button.CommandParameter) ?? false,
            ActiveTicketId = vm.ActiveTicketId,
            SelectedTicketId = vm.SelectedTicket?.Id,
            FormMode = vm.FormMode,
            IsDirty = vm.IsTicketDirty,
            IsPrinting = vm.IsPrinting
        });
    }

    private void WireAutocomplete(Controls.AutoCompleteTextBox box, AutocompleteField field)
    {
        box.ItemCommitted += (_, _) =>
        {
            if (DataContext is not MainViewModel vm)
                return;

            vm.OnSuggestionCommitted(field, box.SelectedItem as AutocompleteSuggestionItem, box.Text);
        };

        box.HistoryRequested += (_, _) =>
        {
            if (DataContext is not MainViewModel vm)
                return;

            _ = vm.ShowFieldHistoryAsync(field, box);
        };
    }

    public void RefreshAutocompleteDisplays()
    {
        CustomerField.RefreshDisplayText();
        VehicleField.RefreshDisplayText();
        CargoField.RefreshDisplayText();
        NotesField.RefreshDisplayText();
    }

    public void FocusCustomerField() => CustomerField.FocusInput();

    public void FocusVehicleField() => VehicleField.FocusInput();

    public void FocusCargoTypeField() => CargoField.FocusInput();

    public void FocusUnitPriceField()
    {
        UnitPriceField.Focus();
        UnitPriceField.SelectAll();
    }

    public void FocusNotesField() => NotesField.FocusInput();

    public void HighlightTicketRow(int ticketId, bool scrollToTop)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (scrollToTop && vm.Tickets.Count > 0)
        {
            TicketsGrid.SelectedItem = vm.Tickets[0];
            TicketsGrid.ScrollIntoView(vm.Tickets[0]);
            return;
        }

        var item = vm.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (item is null)
            return;

        vm.SelectedTicket = item;
        TicketsGrid.SelectedItem = item;
        TicketsGrid.UpdateLayout();
        TicketsGrid.ScrollIntoView(item);
    }

    private async void TicketsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            FindAncestor<Button>(source) is not null)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        if (sender is DataGrid { SelectedItem: WeighTicketListItem item })
            await vm.OpenTicketFromDoubleClickAsync(item);
    }

    private void Weight1Value_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.IsInlineWeightEditEnabled)
            return;
        vm.BeginInlineWeightEditCommand.Execute(1);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Weight1InlineEditBox.Focus();
            Weight1InlineEditBox.SelectAll();
        }), DispatcherPriority.Input);
        e.Handled = true;
    }

    private void Weight2Value_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.IsInlineWeightEditEnabled)
            return;
        vm.BeginInlineWeightEditCommand.Execute(2);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Weight2InlineEditBox.Focus();
            Weight2InlineEditBox.SelectAll();
        }), DispatcherPriority.Input);
        e.Handled = true;
    }

    private async void Weight1InlineEdit_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;
        if (e.Key == Key.Enter)
        {
            await vm.ApplyInlineWeightEditCommand.ExecuteAsync(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelInlineWeightEditCommand.Execute(1);
            e.Handled = true;
        }
    }

    private async void Weight2InlineEdit_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;
        if (e.Key == Key.Enter)
        {
            await vm.ApplyInlineWeightEditCommand.ExecuteAsync(2);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelInlineWeightEditCommand.Execute(2);
            e.Handled = true;
        }
    }

    private async void Weight1InlineEdit_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { IsWeight1InlineEditing: true } vm)
            await vm.ApplyInlineWeightEditCommand.ExecuteAsync(1);
    }

    private async void Weight2InlineEdit_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { IsWeight2InlineEditing: true } vm)
            await vm.ApplyInlineWeightEditCommand.ExecuteAsync(2);
    }

    private void DeleteTicketButton_OnPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        e.Handled = true;

    private static DependencyObject? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
