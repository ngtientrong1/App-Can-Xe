using System.ComponentModel;
using System.Windows;

namespace CanXe.DeviceTester;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is ViewModels.DeviceTesterViewModel vm && !vm.CanCloseApplication())
            e.Cancel = true;
    }
}
