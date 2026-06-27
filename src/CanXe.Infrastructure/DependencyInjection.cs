using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Services;
using CanXe.Infrastructure.Camera;
using CanXe.Infrastructure.Data;
using CanXe.Infrastructure.Repositories;
using CanXe.Infrastructure.Scale;
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

        services.AddSingleton<IPhotoStorageService>(_ => new PhotoStorageService(photoRoot));
        services.AddSingleton<ICameraService>(_ =>
            new SimulatedCameraService(settings.SimulateCameraFailure));
        services.AddSingleton<IPhotoCleanupService>(_ =>
            new PhotoCleanupService(photoRoot, TimeSpan.FromDays(settings.PhotoRetentionDays)));

        if (string.Equals(settings.DeviceMode, "Simulation", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IScaleService, SimulatedScaleService>();
        else
            services.AddSingleton<IScaleService, SimulatedScaleService>(); // Hardware in phase 2

        services.AddScoped<IWeighTicketRepository, WeighTicketRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICargoTypeRepository, CargoTypeRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<WeighTicketService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        await db.Database.EnsureCreatedAsync();

        var cleanup = scope.ServiceProvider.GetRequiredService<IPhotoCleanupService>();
        await cleanup.CleanupOldPhotosAsync();
    }
}
