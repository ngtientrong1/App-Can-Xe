using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Infrastructure.Data;
using CanXe.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[Collection("CanXeDatabase")]
public sealed class CatalogSeedExporterTests
{
    [Fact]
    public async Task Export_KeepsCatalogsAndDropsTicketsAndSettings()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"canxe-seed-src-{Guid.NewGuid():N}.db");
        var seedPath = Path.Combine(Path.GetTempPath(), $"canxe-seed-out-{Guid.NewGuid():N}.db");

        await using (var factory = new TestApplicationFactory(sourcePath))
        {
            await factory.InitializeAsync();
            await using var scope = factory.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
            var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
            scale.SetManualMode(true);

            var draft = new WeighTicketDraft
            {
                DraftCustomer = "Công Ty Đại Lợi",
                DraftCargoType = "Cà tươi",
                DraftVehicle = "51C-123.45"
            };
            scale.SetManualWeightKg(18000m);
            await service.CaptureWeightAsync(draft, 1);
            scale.SetManualWeightKg(9000m);
            await service.CaptureWeightAsync(draft, 2);
            Assert.True((await service.SaveAsync(draft)).Success);

            var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
            db.StationSettings.Add(new StationSettings());
            await db.SaveChangesAsync();
        }

        SqliteConnection.ClearAllPools();

        var result = await CatalogSeedExporter.ExportAsync(sourcePath, seedPath);

        Assert.Equal(1, result.Customers);
        Assert.Equal(1, result.Vehicles);
        Assert.Equal(1, result.CargoTypes);

        await using (var seed = new SqliteConnection($"Data Source={seedPath};Mode=ReadOnly;Pooling=False"))
        {
            await seed.OpenAsync();
            Assert.Equal(0, await ScalarAsync(seed, "SELECT COUNT(*) FROM WeighTickets"));
            Assert.Equal(0, await ScalarAsync(seed, "SELECT COUNT(*) FROM WeighEvents"));
            Assert.Equal(0, await ScalarAsync(seed, "SELECT COUNT(*) FROM StationSettings"));
            Assert.Equal(1, await ScalarAsync(seed, "SELECT COUNT(*) FROM Customers WHERE Name = 'Công Ty Đại Lợi'"));
            Assert.Equal(1, await ScalarAsync(seed, "SELECT COUNT(*) FROM CargoTypes WHERE Name = 'Cà tươi'"));
        }

        // The seed must open and upgrade like a normal database, and the first ticket must number from 1.
        await using var fresh = new TestApplicationFactory(seedPath);
        await fresh.InitializeAsync();
        await using var freshScope = fresh.Provider.CreateAsyncScope();
        var freshDb = freshScope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        Assert.Equal(0, await freshDb.WeighTickets.CountAsync());
        Assert.Equal(1, await freshDb.Customers.CountAsync());
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
