using System.Windows;
using System.Windows.Threading;
using CanXe.DeviceTester.Core;
using CanXe.DeviceTester.Core.Services;
using CanXe.DeviceTester.ViewModels;

namespace CanXe.DeviceTester;

public partial class App : Application
{
    private DeviceTesterViewModel? _viewModel;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            var provider = new WindowsSerialPortProvider();
            var discovery = new WindowsSerialPortDiscoveryService();
            var logWriter = new RawSerialLogWriter();
            var capture = new SerialCaptureService(provider, discovery, logWriter);
            _viewModel = new DeviceTesterViewModel(capture);

            var window = new MainWindow { DataContext = _viewModel };
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            HandleStartupFailure(ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupErrorLogger.Write(e.Exception);

#if DEBUG
        e.Handled = false;
#else
        MessageBox.Show(
            "DeviceTester không thể khởi động. Chi tiết lỗi đã được lưu vào startup-error.log.",
            "CanXe Device Tester",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        if (Current is not null)
            Current.Shutdown(-1);
#endif
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            StartupErrorLogger.Write(ex);
    }

    private static void HandleStartupFailure(Exception ex)
    {
        StartupErrorLogger.Write(ex);
        MessageBox.Show(
            "DeviceTester không thể khởi động. Chi tiết lỗi đã được lưu vào startup-error.log.",
            "CanXe Device Tester",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

#if DEBUG
        throw ex;
#else
        Current?.Shutdown(-1);
#endif
    }
}
