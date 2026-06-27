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
    }

    public void FocusCustomerField()
    {
        CustomerField.Focus();
        CustomerField.SelectAll();
    }

    private async void TicketsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (sender is DataGrid grid && grid.SelectedItem is WeighTicketListItem item)
            await vm.ContinueTicketCommand.ExecuteAsync(item);
    }

    private void SuggestionList_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not ListBox listBox)
            return;

        if (listBox.SelectedItem is not string selected)
            return;

        if (listBox.Tag as string == "Customer")
            vm.CustomerName = selected;
        else if (listBox.Tag as string == "CargoType")
            vm.CargoTypeName = selected;
    }
}
