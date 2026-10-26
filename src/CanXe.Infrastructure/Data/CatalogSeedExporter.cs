using Microsoft.Data.Sqlite;

namespace CanXe.Infrastructure.Data;

/// <summary>
/// Builds a seed database for a fresh install: a copy of the live database that keeps only the
/// catalogs (customers, vehicles, cargo types) and drops every ticket, event, audit/print history,
/// ticket counter and device/station/print setting, so a new machine starts clean with defaults.
/// </summary>
public static class CatalogSeedExporter
{
    public const string SeedFileName = "canxe-seed.db";

    private static readonly string[] TablesToEmpty =
    [
        "WeighEvents", "WeighTickets", "AuditLogs", "PrintJobHistories", "TicketSequences",
        "StationSettings", "ScaleDeviceSettings", "CameraDeviceSettings", "PrintSettings"
    ];

    public sealed record Result(int Customers, int Vehicles, int CargoTypes);

    public static async Task<Result> ExportAsync(
        string sourceDbPath, string destDbPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceDbPath))
            throw new FileNotFoundException("Source database not found.", sourceDbPath);

        var destDir = Path.GetDirectoryName(Path.GetFullPath(destDbPath));
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);
        if (File.Exists(destDbPath))
            File.Delete(destDbPath);

        // VACUUM INTO writes a consistent copy even while the app has the source open.
        await using (var source = new SqliteConnection($"Data Source={sourceDbPath};Mode=ReadOnly;Pooling=False"))
        {
            await source.OpenAsync(cancellationToken);
            await using var copy = source.CreateCommand();
            copy.CommandText = $"VACUUM INTO '{destDbPath.Replace("'", "''")}'";
            await copy.ExecuteNonQueryAsync(cancellationToken);
        }

        Result result;
        await using (var dest = new SqliteConnection($"Data Source={destDbPath};Pooling=False"))
        {
            await dest.OpenAsync(cancellationToken);

            foreach (var table in TablesToEmpty)
            {
                if (await TableExistsAsync(dest, table, cancellationToken))
                    await ExecuteAsync(dest, $"DELETE FROM \"{table}\"", cancellationToken);
            }

            // Hidden catalog entries are not worth carrying over; detach vehicles from hidden customers first.
            await ExecuteAsync(dest,
                "UPDATE Vehicles SET LastCustomerId = NULL WHERE LastCustomerId IN (SELECT Id FROM Customers WHERE IsDeleted = 1)",
                cancellationToken);
            await ExecuteAsync(dest, "DELETE FROM Customers WHERE IsDeleted = 1", cancellationToken);
            await ExecuteAsync(dest, "DELETE FROM Vehicles WHERE IsDeleted = 1", cancellationToken);
            await ExecuteAsync(dest, "DELETE FROM CargoTypes WHERE IsDeleted = 1", cancellationToken);

            if (await TableExistsAsync(dest, "sqlite_sequence", cancellationToken))
            {
                var names = string.Join(",", TablesToEmpty.Select(t => $"'{t}'"));
                await ExecuteAsync(dest, $"DELETE FROM sqlite_sequence WHERE name IN ({names})", cancellationToken);
            }

            result = new Result(
                await CountAsync(dest, "Customers", cancellationToken),
                await CountAsync(dest, "Vehicles", cancellationToken),
                await CountAsync(dest, "CargoTypes", cancellationToken));

            await ExecuteAsync(dest, "VACUUM", cancellationToken);
        }

        SqliteConnection.ClearAllPools();
        return result;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
