using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        if (e.Key == Key.Escape && DataContext is MainViewModel { IsTicketPreviewVisible: true } vm)
        {
            vm.CloseTicketPreviewCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void WireAutocomplete(Controls.AutoCompleteTextBox box, AutocompleteField field)
    {
        box.ItemCommitted += (_, _) =>
        {
            if (DataContext is not MainViewModel vm)
                return;

            vm.OnSuggestionCommitted(field, box.SelectedItem as AutocompleteSuggestionItem, box.Text);
        };
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
        if (DataContext is not MainViewModel vm)
            return;

        if (!vm.CanLoadTicketIntoForm)
            return;

        if (sender is DataGrid grid && grid.SelectedItem is WeighTicketListItem item)
            await vm.BeginEditTicketCommand.ExecuteAsync(item);
    }
}
