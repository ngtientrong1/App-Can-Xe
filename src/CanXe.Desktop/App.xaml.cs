using System.IO;
using System.Windows;
using CanXe.Application.Interfaces;
using CanXe.Desktop.Services;
using CanXe.Desktop.ViewModels;
using CanXe.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanXe.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe");
        Directory.CreateDirectory(appData);

        var dbPath = Path.Combine(appData, "canxe.db");
        var photoRoot = Path.Combine(appData, "Photos");
        Directory.CreateDirectory(photoRoot);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddCanXeInfrastructure(dbPath, photoRoot);
                services.AddSingleton<IUserNotificationService, WpfNotificationService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();
        await DependencyInjection.InitializeDatabaseAsync(_host.Services);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var viewModel = _host.Services.GetRequiredService<MainViewModel>();
        mainWindow.DataContext = viewModel;
        await viewModel.InitializeAsync();
        mainWindow.Show();
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
}
