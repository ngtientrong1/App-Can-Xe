using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CanXe.Application.Models;

namespace CanXe.Desktop.Views.Catalogs;

public partial class CatalogView
{
    public CatalogView() => InitializeComponent();
}

public sealed class CatalogTabColumnVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CatalogTab tab || parameter is not string columnKey)
            return Visibility.Collapsed;

        var visible = (tab, columnKey) switch
        {
            (CatalogTab.Customer, "CustomerName") => true,
            (CatalogTab.Customer, "CustomerPhone") => true,
            (CatalogTab.Customer, "CustomerAddress") => true,
            (CatalogTab.Vehicle, "VehiclePlate") => true,
            (CatalogTab.Vehicle, "VehicleOwner") => true,
            (CatalogTab.CargoType, "CargoName") => true,
            (CatalogTab.CargoType, "CargoPrice") => true,
            (CatalogTab.CargoType, "CargoUnit") => true,
            _ => false
        };

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
