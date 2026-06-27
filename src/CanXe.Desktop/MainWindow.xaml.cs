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
        VehicleField.ItemCommitted += async (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                await vm.OnVehicleCommittedAsync(VehicleField.Text);
        };
    }

    public void FocusCustomerField() => CustomerField.FocusInput();

    private async void TicketsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (sender is DataGrid grid && grid.SelectedItem is WeighTicketListItem item)
            await vm.OpenTicketDetailCommand.ExecuteAsync(item);
    }
}
