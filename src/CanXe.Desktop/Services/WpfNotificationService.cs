using System.Windows;
using CanXe.Application.Interfaces;

namespace CanXe.Desktop.Services;

public sealed class WpfNotificationService : IUserNotificationService
{
    public void Notify(string message)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            Publish(message);
        else
            System.Windows.Application.Current?.Dispatcher.Invoke(() => Publish(message));
    }

    private static void Publish(string message)
    {
        if (System.Windows.Application.Current?.MainWindow?.DataContext is ViewModels.MainViewModel vm)
            vm.StatusMessage = message;
    }
}
