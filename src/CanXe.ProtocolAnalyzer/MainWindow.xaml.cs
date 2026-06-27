using System.Windows;
using System.Windows.Input;

namespace CanXe.ProtocolAnalyzer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AllowDrop = true;
        Drop += OnDrop;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (DataContext is ViewModels.ProtocolAnalyzerViewModel vm)
            vm.ImportDroppedFiles(files);
    }
}
