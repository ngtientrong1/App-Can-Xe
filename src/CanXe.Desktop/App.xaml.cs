using System.IO;
using System.Text.Json;
using System.Windows;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Desktop.Services;
using CanXe.Desktop.ViewModels;
using CanXe.Infrastructure;
using CanXe.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanXe.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            RegisterGlobalExceptionHandlers(this);

            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CanXe");
            Directory.CreateDirectory(appData);

            var dbPath = Path.Combine(appData, "canxe.db");
            var photoRoot = Path.Combine(appData, "Photos");
            Directory.CreateDirectory(photoRoot);

            var settings = LoadSettings();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddCanXeInfrastructure(settings, dbPath, photoRoot);
                    services.AddSingleton<IUserNotificationService, WpfNotificationService>();
                    services.AddSingleton<IUiFocusService, WpfUiFocusService>();
                    services.AddSingleton<PrintCommandLogger>();
                    services.AddSingleton<IPrintNotificationService, WpfPrintNotificationService>();
                    services.AddSingleton<IWeighTicketPrintSubmissionService, WeighTicketPrintSubmissionService>();
                    services.AddSingleton<IWeighTicketPrintWorkflow, WeighTicketPrintWorkflow>();
                    services.AddSingleton<IWeighTicketDocumentFactory, WpfWeighTicketDocumentFactory>();
                    services.AddSingleton<IWeighTicketDocumentBuilder, WpfWeighTicketDocumentBuilder>();
                    services.AddSingleton<IPrinterCapabilityService, PrinterCapabilityService>();
                    services.AddSingleton<IWeighTicketPrintService, WpfWeighTicketPrintService>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<DeveloperViewModel>();
                    services.AddSingleton<CatalogViewModel>();
                    services.AddSingleton<ReportViewModel>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();
            await DependencyInjection.InitializeDatabaseAsync(_host.Services, dbPath);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            mainWindow.DataContext = viewModel;
            await viewModel.InitializeAsync();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            StartupErrorLogger.Write(ex);
            MessageBox.Show(
                "Không thể khởi động giao diện CanXe. Chi tiết đã được ghi vào startup-error.log.",
                "CanXe — Lỗi khởi động",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            throw;
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var vm = _host.Services.GetService<MainViewModel>();
            if (vm is not null)
                await vm.DisposeAsync();

            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static AppSettings LoadSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            return new AppSettings();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new AppSettings();
    }

    private static void RegisterGlobalExceptionHandlers(App app)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppExceptionLogger.WriteError("AppDomain.UnhandledException", ex);
                WeighWorkflowLogger.Write("UNHANDLED_EXCEPTION", ex.GetType().Name);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppExceptionLogger.WriteError("TaskScheduler.UnobservedTaskException", e.Exception);
            WeighWorkflowLogger.Write("UNOBSERVED_TASK_EXCEPTION", e.Exception.GetType().Name);
            e.SetObserved();
        };

        app.DispatcherUnhandledException += (_, e) =>
        {
            if (AppExceptionLogger.ConsumeSaveErrorHandled())
            {
                e.Handled = true;
                return;
            }

            AppExceptionLogger.WriteError("DispatcherUnhandledException", e.Exception);
            WeighWorkflowLogger.Write("DISPATCHER_UNHANDLED_EXCEPTION", e.Exception.GetType().Name);
            MessageBox.Show(
                "Đã xảy ra lỗi không mong muốn. Chi tiết đã được ghi vào errors.log.",
                "CanXe — Lỗi",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}
