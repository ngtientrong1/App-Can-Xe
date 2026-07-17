using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CanXe.Application.Models;

namespace CanXe.Desktop.Views.Catalogs;

public partial class CatalogView
{
    public CatalogView() => InitializeComponent();
}

/// <summary>
/// Shows a catalog DataGrid only when <see cref="CatalogTab"/> matches ConverterParameter.
/// Grid-level visibility is reliable; per-column Visibility on DataGrid columns is not.
/// </summary>
public sealed class CatalogTabGridVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CatalogTab selectedTab || parameter is not string tabKey)
            return Visibility.Collapsed;

        var targetTab = tabKey switch
        {
            "Customer" => CatalogTab.Customer,
            "Vehicle" => CatalogTab.Vehicle,
            "CargoType" => CatalogTab.CargoType,
            _ => (CatalogTab)(-1)
        };

        return selectedTab == targetTab ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
