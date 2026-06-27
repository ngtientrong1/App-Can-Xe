using System.Windows;
using CanXe.Desktop.ViewModels;

namespace CanXe.Desktop.Services;

public sealed class WpfUiFocusService : IUiFocusService
{
    private MainWindow? _window;

    public void RegisterWindow(MainWindow window) => _window = window;

    public void FocusCustomerField() =>
        _window?.FocusCustomerField();
}
