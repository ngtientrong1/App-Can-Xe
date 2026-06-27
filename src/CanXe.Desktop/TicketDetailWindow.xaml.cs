using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CanXe.Application.Models;

namespace CanXe.Desktop;

public partial class TicketDetailWindow : Window
{
    public TicketDetailWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.TicketDetailViewModel vm)
            return;

        LoadPhoto(vm.Detail.Weight1PhotoPath, vm.Detail.Weight1PhotoAvailable, Weight1Image, Weight1PhotoPlaceholder);
        LoadPhoto(vm.Detail.Weight2PhotoPath, vm.Detail.Weight2PhotoAvailable, Weight2Image, Weight2PhotoPlaceholder);

        UnitPriceBlock.Text = vm.Detail.UnitPriceVndPerKg is { } p and > 0
            ? $"{p:N0} VNĐ/kg"
            : "Cân dịch vụ";
    }

    private static void LoadPhoto(string? path, bool available, System.Windows.Controls.Image image, System.Windows.Controls.TextBlock placeholder)
    {
        if (!available || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            image.Visibility = Visibility.Collapsed;
            placeholder.Visibility = Visibility.Visible;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        image.Source = bitmap;
        image.Visibility = Visibility.Visible;
        placeholder.Visibility = Visibility.Collapsed;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.TicketDetailViewModel vm &&
            Owner?.DataContext is ViewModels.MainViewModel main &&
            vm.Detail.CanContinue)
        {
            _ = main.ContinueTicketCommand.ExecuteAsync(new WeighTicketListItem
            {
                Id = vm.Detail.Id,
                DisplayNumber = vm.Detail.DisplayNumber,
                TicketDateTime = vm.Detail.TicketDateTime,
                EventCount = vm.Detail.EventCount
            });
        }

        Close();
    }
}
