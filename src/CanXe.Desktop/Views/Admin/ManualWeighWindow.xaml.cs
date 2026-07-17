using System.Windows;

namespace CanXe.Desktop.Views.Admin;

public partial class ManualWeighWindow : Window
{
    public ManualWeighWindow(bool suggestSequence1, bool suggestSequence2)
    {
        InitializeComponent();
        Sequence1Radio.IsChecked = suggestSequence1 || !suggestSequence2;
        Sequence2Radio.IsChecked = !suggestSequence1 && suggestSequence2;
        Loaded += (_, _) => WeightTextBox.Focus();
    }

    public int? SelectedSequence { get; private set; }
    public string? WeightText { get; private set; }
    public string? ReasonText { get; private set; }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (Sequence1Radio.IsChecked != true && Sequence2Radio.IsChecked != true)
        {
            MessageBox.Show(this, "Vui lòng chọn lần cân.", "Nhập cân tay", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedSequence = Sequence1Radio.IsChecked == true ? 1 : 2;
        WeightText = WeightTextBox.Text;
        ReasonText = ReasonTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
