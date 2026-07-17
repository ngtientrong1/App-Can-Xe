namespace CanXe.Infrastructure;

using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Infrastructure.Data;
using CanXe.Infrastructure.Diagnostics;
using CanXe.Infrastructure.Repositories;
using CanXe.Infrastructure.Device;
using CanXe.Infrastructure.Scale;
using CanXe.Infrastructure.Security;
using CanXe.Infrastructure.Services;
using CanXe.ScaleProtocol.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddCanXeInfrastructure(
        this IServiceCollection services,
        AppSettings settings,
        string databasePath,
        string photoRoot)
    {
        services.AddSingleton(settings);

        services.AddDbContext<CanXeDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        services.AddSingleton<ISecretProtector, DpApiSecretProtector>();
        services.AddSingleton<IBuildInfoProvider, BuildInfoProvider>();
        services.AddSingleton<ISystemDiagnosticsService, SystemDiagnosticsService>();
        services.AddScoped<DiagnosticsPhaseRunner>();
        services.AddSingleton<AppPaths>(_ => new AppPaths { DatabasePath = databasePath, PhotoRoot = photoRoot });
        services.AddSingleton<IScaleConnectionTester>(sp =>
            new ScaleConnectionTester(sp.GetRequiredService<AppSettings>()));

        services.AddSingleton<IScaleSerialReader, WindowsScaleSerialReader>();
        services.AddSingleton<CompositeScaleService>(sp =>
            new CompositeScaleService(sp.GetRequiredService<IScaleSerialReader>()));
        services.AddSingleton<IScaleService>(sp => sp.GetRequiredService<CompositeScaleService>());
        services.AddSingleton<IHardwareScaleDiagnostics>(sp => sp.GetRequiredService<CompositeScaleService>());

        services.AddScoped<IWeighTicketRepository, WeighTicketRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICargoTypeRepository, CargoTypeRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IStationSettingsRepository, StationSettingsRepository>();
        services.AddScoped<IScaleDeviceSettingsRepository, ScaleDeviceSettingsRepository>();
        services.AddScoped<ICameraDeviceSettingsRepository, CameraDeviceSettingsRepository>();
        services.AddScoped<IPrintSettingsRepository, PrintSettingsRepository>();
        services.AddScoped<IPrintJobHistoryRepository, PrintJobHistoryRepository>();
        services.AddSingleton<IPrinterCapabilityService, MockPrinterCapabilityService>();
        services.AddScoped<WeighTicketService>();
        services.AddScoped<FastEntrySearchService>();
        services.AddScoped<TicketUpdateService>();
        services.AddScoped<TicketDeleteService>();
        services.AddScoped<ITicketDeleteService>(sp => sp.GetRequiredService<TicketDeleteService>());
        services.AddSingleton<IAdminAuthorizationService, AdminAuthorizationService>();
        services.AddSingleton<IUserPermissionService, UserPermissionService>();
        services.AddSingleton<IDeveloperAuthorizationService>(sp =>
            new DeveloperAuthorizationService(sp.GetRequiredService<IUserPermissionService>()));
        services.AddScoped<IReportService, ReportService>();
        services.AddSingleton<IExcelReportExporter, ExcelReportExporter>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<StationSettingsService>();
        services.AddScoped<PrintSettingsService>();
        services.AddSingleton<IBackupService, BackupService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(IServiceProvider services, string databasePath)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        await DatabaseUpgrader.UpgradeAsync(db, databasePath);
    }
}
