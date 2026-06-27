using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Services;
using CanXe.Infrastructure.Camera;
using CanXe.Infrastructure.Data;
using CanXe.Infrastructure.Device;
using CanXe.Infrastructure.Repositories;
using CanXe.Infrastructure.Scale;
using CanXe.Infrastructure.Security;
using CanXe.ScaleProtocol.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Infrastructure;

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
        services.AddSingleton<ICameraConnectionTester>(_ =>
            new MockCameraConnectionTester(settings.SimulateCameraFailure));
        services.AddSingleton<IScaleConnectionTester>(sp =>
            new ScaleConnectionTester(sp.GetRequiredService<AppSettings>()));

        services.AddSingleton<IPhotoStorageService>(_ => new PhotoStorageService(photoRoot));
        services.AddSingleton<ICameraService>(_ =>
            new SimulatedCameraService(settings.SimulateCameraFailure));
        services.AddSingleton<IPhotoCleanupService>(_ =>
            new PhotoCleanupService(photoRoot, TimeSpan.FromDays(settings.PhotoRetentionDays)));

        services.AddSingleton<IScaleSerialReader, WindowsScaleSerialReader>();
        services.AddSingleton<CompositeScaleService>(sp =>
        {
            return new CompositeScaleService(sp.GetRequiredService<IScaleSerialReader>());
        });
        services.AddSingleton<IScaleService>(sp => sp.GetRequiredService<CompositeScaleService>());
        services.AddSingleton<IHardwareScaleDiagnostics>(sp => sp.GetRequiredService<CompositeScaleService>());

        services.AddScoped<IWeighTicketRepository, WeighTicketRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICargoTypeRepository, CargoTypeRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IStationSettingsRepository, StationSettingsRepository>();
        services.AddScoped<IScaleDeviceSettingsRepository, ScaleDeviceSettingsRepository>();
        services.AddScoped<ICameraDeviceSettingsRepository, CameraDeviceSettingsRepository>();
        services.AddScoped<WeighTicketService>();
        services.AddScoped<FastEntrySearchService>();
        services.AddScoped<TicketUpdateService>();
        services.AddScoped<StationSettingsService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(IServiceProvider services, string databasePath)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        await DatabaseUpgrader.UpgradeAsync(db, databasePath);

        var cleanup = scope.ServiceProvider.GetRequiredService<IPhotoCleanupService>();
        await cleanup.CleanupOldPhotosAsync();
    }
}
