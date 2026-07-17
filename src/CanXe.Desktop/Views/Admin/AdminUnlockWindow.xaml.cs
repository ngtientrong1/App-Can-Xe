using System.Windows;
using System.Windows.Controls;

namespace CanXe.Desktop.Views.Admin;

public partial class AdminUnlockWindow : Window
{
    public AdminUnlockWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordBox.Focus();
    }

    public string? Password { get; private set; }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        Password = PasswordBox.Password;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void PasswordBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            Unlock_Click(sender, e);
    }
}
