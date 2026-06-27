using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CanXe.Application.Models;
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
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WireAutocomplete(CustomerField, AutocompleteField.Customer);
        WireAutocomplete(VehicleField, AutocompleteField.Vehicle);
        WireAutocomplete(CargoField, AutocompleteField.CargoType);
        WireAutocomplete(NotesField, AutocompleteField.Notes);
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

    private async void TicketsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (sender is DataGrid grid && grid.SelectedItem is WeighTicketListItem item)
            await vm.OpenTicketDetailCommand.ExecuteAsync(item);
    }
}
