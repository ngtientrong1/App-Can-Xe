using System.Globalization;
using System.Windows.Data;
using CanXe.Application.Services;

namespace CanXe.Desktop.Converters;

public sealed class VietnameseNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = parameter as string ?? "weight";
        return kind switch
        {
            "money" => value switch
            {
                decimal d => VietnameseNumberFormatter.FormatMoney(d),
                null => VietnameseNumberFormatter.FormatMoney((decimal?)null),
                _ => "—"
            },
            "integer" => value switch
            {
                int i => VietnameseNumberFormatter.FormatInteger(i),
                decimal d => VietnameseNumberFormatter.FormatInteger(d),
                null => VietnameseNumberFormatter.FormatInteger((decimal?)null),
                _ => "—"
            },
            _ => value switch
            {
                decimal d => VietnameseNumberFormatter.FormatWeight(d),
                null => VietnameseNumberFormatter.FormatWeight((decimal?)null),
                _ => "—"
            }
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
