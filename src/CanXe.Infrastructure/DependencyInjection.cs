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
        string databasePath,
        string photoRoot)
    {
        services.AddDbContext<CanXeDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        services.AddSingleton<IScaleService, SimulatedScaleService>();
        services.AddSingleton<ICameraService>(_ => new SimulatedCameraService(photoRoot));
        services.AddSingleton<IPhotoCleanupService>(_ => new PhotoCleanupService(photoRoot));

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
