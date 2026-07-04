using System.IO;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.ViewModels;
using CanXe.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.Tests.Support;

public sealed class DesktopTestHost : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly string _photoRoot;
    private readonly bool _deleteOnDispose;

    public DesktopTestHost(
        AppSettings settings,
        string? databasePath = null,
        string? photoRootPath = null,
        bool deleteOnDispose = true)
    {
        Settings = settings;
        _deleteOnDispose = deleteOnDispose;
        _dbPath = databasePath ?? Path.Combine(Path.GetTempPath(), $"canxe-desktop-test-{Guid.NewGuid():N}.db");
        _photoRoot = photoRootPath ?? Path.Combine(Path.GetTempPath(), $"canxe-desktop-photos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_photoRoot);

        AppPaths = new AppPaths
        {
            DatabasePath = _dbPath,
            PhotoRoot = _photoRoot
        };

        var services = new ServiceCollection();
        services.AddCanXeInfrastructure(settings, _dbPath, _photoRoot);
        services.AddSingleton<IUiFocusService, NoOpUiFocusService>();
        services.AddSingleton<PrintCommandLogger>();
        services.AddSingleton<IPrintNotificationService, WpfPrintNotificationService>();
        services.AddSingleton<IWeighTicketPrintSubmissionService, WeighTicketPrintSubmissionService>();
        services.AddSingleton<IWeighTicketPrintWorkflow, WeighTicketPrintWorkflow>();
        services.AddSingleton<IWeighTicketDocumentFactory, WpfWeighTicketDocumentFactory>();
        services.AddSingleton<IWeighTicketDocumentBuilder, WpfWeighTicketDocumentBuilder>();
        services.AddSingleton<IPrinterCapabilityService, PrinterCapabilityService>();
        services.AddSingleton<IWeighTicketPrintService, WpfWeighTicketPrintService>();
        services.AddScoped<SettingsViewModel>();
        services.AddScoped<DeveloperViewModel>();
        Provider = services.BuildServiceProvider();
    }

    public AppSettings Settings { get; }

    public AppPaths AppPaths { get; }

    public ServiceProvider Provider { get; }

    private IServiceScope? _viewModelScope;
    private MainViewModel? _activeViewModel;

    public async Task<(MainViewModel ViewModel, SettingsViewModel SettingsViewModel)> CreateMainViewModelForBindingSmokeAsync()
    {
        await DependencyInjection.InitializeDatabaseAsync(Provider, _dbPath);
        _viewModelScope?.Dispose();
        _viewModelScope = Provider.CreateScope();
        var scope = _viewModelScope.ServiceProvider;
        var settingsViewModel = scope.GetRequiredService<SettingsViewModel>();
        await settingsViewModel.LoadAsync(_dbPath);
        _activeViewModel = new MainViewModel(
            scope.GetRequiredService<WeighTicketService>(),
            scope.GetRequiredService<FastEntrySearchService>(),
            scope.GetRequiredService<IScaleService>(),
            scope.GetRequiredService<IHardwareScaleDiagnostics>(),
            scope.GetRequiredService<StationSettingsService>(),
            Provider.GetRequiredService<IUiFocusService>(),
            scope.GetRequiredService<IWeighTicketPrintService>(),
            scope.GetRequiredService<IWeighTicketPrintWorkflow>(),
            scope.GetRequiredService<IWeighTicketDocumentFactory>(),
            scope.GetRequiredService<PrintSettingsService>(),
            Provider.GetRequiredService<PrintCommandLogger>(),
            Provider.GetRequiredService<IPrintNotificationService>(),
            scope.GetRequiredService<ITicketDeleteService>(),
            scope.GetRequiredService<IDeveloperAuthorizationService>(),
            Settings,
            settingsViewModel,
            scope.GetRequiredService<DeveloperViewModel>(),
            AppPaths);
        return (_activeViewModel, settingsViewModel);
    }

    public async Task<(MainViewModel ViewModel, SettingsViewModel SettingsViewModel)> CreateMainViewModelForBindingSmokeAsync(
        IWeighTicketPrintWorkflow printWorkflow,
        IPrintNotificationService printNotificationService)
    {
        await DependencyInjection.InitializeDatabaseAsync(Provider, _dbPath);
        _viewModelScope?.Dispose();
        _viewModelScope = Provider.CreateScope();
        var scope = _viewModelScope.ServiceProvider;
        var settingsViewModel = scope.GetRequiredService<SettingsViewModel>();
        await settingsViewModel.LoadAsync(_dbPath);
        _activeViewModel = new MainViewModel(
            scope.GetRequiredService<WeighTicketService>(),
            scope.GetRequiredService<FastEntrySearchService>(),
            scope.GetRequiredService<IScaleService>(),
            scope.GetRequiredService<IHardwareScaleDiagnostics>(),
            scope.GetRequiredService<StationSettingsService>(),
            Provider.GetRequiredService<IUiFocusService>(),
            scope.GetRequiredService<IWeighTicketPrintService>(),
            printWorkflow,
            scope.GetRequiredService<IWeighTicketDocumentFactory>(),
            scope.GetRequiredService<PrintSettingsService>(),
            Provider.GetRequiredService<PrintCommandLogger>(),
            printNotificationService,
            scope.GetRequiredService<ITicketDeleteService>(),
            scope.GetRequiredService<IDeveloperAuthorizationService>(),
            Settings,
            settingsViewModel,
            scope.GetRequiredService<DeveloperViewModel>(),
            AppPaths);
        return (_activeViewModel, settingsViewModel);
    }

    public async ValueTask DisposeAsync()
    {
        if (_activeViewModel is not null)
        {
            await _activeViewModel.DisposeAsync();
            _activeViewModel = null;
        }

        _viewModelScope?.Dispose();
        _viewModelScope = null;
        await Provider.DisposeAsync();
        if (_deleteOnDispose)
        {
            TryDelete(_dbPath);
            TryDeleteDirectory(_photoRoot);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
