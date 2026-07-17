using System.Windows;
using CanXe.Application.Models;

namespace CanXe.Desktop.Views.Catalogs;

public partial class CatalogEditWindow : Window
{
    public CatalogEditWindow(CatalogTab tab, CatalogRowItem? row, bool allowDelete = false)
    {
        InitializeComponent();
        Tab = tab;
        Title = row is null ? "Thêm danh mục" : "Sửa danh mục";
        DeleteRequested = false;

        CustomerPanel.Visibility = tab == CatalogTab.Customer ? Visibility.Visible : Visibility.Collapsed;
        VehiclePanel.Visibility = tab == CatalogTab.Vehicle ? Visibility.Visible : Visibility.Collapsed;
        CargoPanel.Visibility = tab == CatalogTab.CargoType ? Visibility.Visible : Visibility.Collapsed;

        DeleteButton.Visibility = allowDelete && row is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (row is null)
            return;

        switch (tab)
        {
            case CatalogTab.Customer:
                CustomerIdBox.Text = row.Id.ToString();
                CustomerNameBox.Text = row.PrimaryName;
                CustomerPhoneBox.Text = row.Phone;
                CustomerAddressBox.Text = row.Address;
                CustomerNoteBox.Text = row.Note;
                CustomerActiveBox.IsChecked = row.IsActive;
                break;
            case CatalogTab.Vehicle:
                VehicleIdBox.Text = row.Id.ToString();
                VehiclePlateBox.Text = row.PrimaryName;
                VehicleOwnerBox.Text = row.SecondaryName;
                VehicleNoteBox.Text = row.Note;
                VehicleActiveBox.IsChecked = row.IsActive;
                break;
            case CatalogTab.CargoType:
                CargoIdBox.Text = row.Id.ToString();
                CargoNameBox.Text = row.PrimaryName;
                CargoPriceBox.Text = row.DefaultUnitPrice?.ToString("0") ?? string.Empty;
                CargoUnitBox.Text = row.Unit ?? "kg";
                CargoNoteBox.Text = row.Note;
                CargoActiveBox.IsChecked = row.IsActive;
                break;
        }
    }

    public CatalogTab Tab { get; }

    public bool DeleteRequested { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested = false;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested = false;
        DialogResult = false;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
        DialogResult = true;
    }

    public CustomerCatalogEdit ToCustomerEdit() => new()
    {
        Id = int.TryParse(CustomerIdBox.Text, out var id) ? id : 0,
        Name = CustomerNameBox.Text,
        Phone = CustomerPhoneBox.Text,
        Address = CustomerAddressBox.Text,
        Note = CustomerNoteBox.Text,
        IsActive = CustomerActiveBox.IsChecked == true
    };

    public VehicleCatalogEdit ToVehicleEdit() => new()
    {
        Id = int.TryParse(VehicleIdBox.Text, out var id) ? id : 0,
        LicensePlate = VehiclePlateBox.Text,
        OwnerName = VehicleOwnerBox.Text,
        Note = VehicleNoteBox.Text,
        IsActive = VehicleActiveBox.IsChecked == true
    };

    public CargoCatalogEdit ToCargoEdit()
    {
        decimal? price = null;
        if (decimal.TryParse(CargoPriceBox.Text, out var parsed) && parsed > 0)
            price = parsed;

        return new CargoCatalogEdit
        {
            Id = int.TryParse(CargoIdBox.Text, out var id) ? id : 0,
            Name = CargoNameBox.Text,
            DefaultUnitPrice = price,
            Unit = string.IsNullOrWhiteSpace(CargoUnitBox.Text) ? "kg" : CargoUnitBox.Text,
            Note = CargoNoteBox.Text,
            IsActive = CargoActiveBox.IsChecked == true
        };
    }
}
