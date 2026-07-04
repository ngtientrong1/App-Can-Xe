using System.Windows;
using System.Windows.Controls;
using CanXe.Application.Models;

namespace CanXe.Desktop.Windows;

public sealed class PrintDirtyChoiceDialog : Window
{
    public PrintDirtyChoice Choice { get; private set; } = PrintDirtyChoice.Cancel;

    public PrintDirtyChoiceDialog()
    {
        Title = "Phiếu chưa lưu";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var message = new TextBlock
        {
            Text = "Phiếu đang có thay đổi chưa lưu.",
            Margin = new Thickness(16, 16, 16, 8),
            TextWrapping = TextWrapping.Wrap
        };

        var currentButton = new Button
        {
            Content = "IN DỮ LIỆU HIỆN TẠI",
            MinWidth = 160,
            Margin = new Thickness(4)
        };
        currentButton.Click += (_, _) =>
        {
            Choice = PrintDirtyChoice.PrintCurrent;
            DialogResult = true;
            Close();
        };

        var savedButton = new Button
        {
            Content = "IN BẢN ĐÃ LƯU",
            MinWidth = 140,
            Margin = new Thickness(4)
        };
        savedButton.Click += (_, _) =>
        {
            Choice = PrintDirtyChoice.PrintSaved;
            DialogResult = true;
            Close();
        };

        var cancelButton = new Button
        {
            Content = "HỦY",
            MinWidth = 90,
            Margin = new Thickness(4),
            IsCancel = true
        };
        cancelButton.Click += (_, _) =>
        {
            Choice = PrintDirtyChoice.Cancel;
            DialogResult = false;
            Close();
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 8, 16, 16)
        };
        buttons.Children.Add(currentButton);
        buttons.Children.Add(savedButton);
        buttons.Children.Add(cancelButton);

        Content = new StackPanel
        {
            Children = { message, buttons }
        };
    }
}
