using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Data;

/// <summary>
/// Brings a database made by an older version up to the current model without losing data. EnsureCreated only
/// builds a database that does not exist yet, so every column or table added since the first release is added here.
/// Each step is idempotent: it checks first and only adds what is missing.
/// </summary>
public static class SchemaUpgrader
{
    private sealed record ColumnSpec(string Table, string Column, string Definition);

    private static readonly ColumnSpec[] Columns =
    [
        new("Tickets", "GrossWeightKg", "TEXT NULL"),
        new("Tickets", "TareWeightKg", "TEXT NULL"),
        new("Tickets", "NetWeightKg", "TEXT NULL"),
        new("Tickets", "AwaitingSecondWeigh", "INTEGER NOT NULL DEFAULT 0"),
        new("Tickets", "FirstWeightKg", "TEXT NULL"),
        new("Tickets", "Notes", "TEXT NULL"),
        new("Payments", "BankName", "TEXT NULL"),
        new("Payments", "ReceivedBy", "TEXT NULL"),
        new("Payments", "PaidBy", "TEXT NULL"),
        new("Payments", "PeriodId", "INTEGER NULL")
    ];

    private const string CreatePeriods = """
        CREATE TABLE IF NOT EXISTS "Periods" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Periods" PRIMARY KEY AUTOINCREMENT,
            "CustomerId" INTEGER NOT NULL,
            "SeasonId" INTEGER NOT NULL,
            "FromDate" TEXT NOT NULL,
            "ToDate" TEXT NOT NULL,
            "ClosedAt" TEXT NOT NULL,
            "ClosedBy" TEXT NOT NULL,
            CONSTRAINT "FK_Periods_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
        )
        """;

    private static readonly string[] Indexes =
    [
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Periods_CustomerId_SeasonId_FromDate" ON "Periods" ("CustomerId", "SeasonId", "FromDate")""",
        """CREATE INDEX IF NOT EXISTS "IX_Payments_PeriodId" ON "Payments" ("PeriodId")"""
    ];

    /// <summary>Descriptions of what an upgrade would change; empty when the database is current or not built yet.</summary>
    public static async Task<IReadOnlyList<string>> PendingChangesAsync(DbContext db, CancellationToken ct = default)
    {
        await using var scope = await ConnectionScope.OpenAsync(db, ct);
        return await FindPendingAsync(scope.Connection, ct);
    }

    /// <summary>Applies every missing change and returns what was added.</summary>
    public static async Task<IReadOnlyList<string>> ApplyAsync(DbContext db, CancellationToken ct = default)
    {
        await using var scope = await ConnectionScope.OpenAsync(db, ct);
        var connection = scope.Connection;
        var pending = await FindPendingAsync(connection, ct);
        if (pending.Count == 0)
            return pending;

        await using var transaction = await connection.BeginTransactionAsync(ct);
        foreach (var column in Columns)
        {
            if (await TableExistsAsync(connection, column.Table, ct) && !await ColumnExistsAsync(connection, column.Table, column.Column, ct))
                await ExecuteAsync(connection, $"ALTER TABLE \"{column.Table}\" ADD COLUMN \"{column.Column}\" {column.Definition}", ct);
        }

        if (await TableExistsAsync(connection, "Customers", ct))
        {
            await ExecuteAsync(connection, CreatePeriods, ct);
            foreach (var index in Indexes)
            {
                if (await TableExistsAsync(connection, index.Contains("\"Periods\"") ? "Periods" : "Payments", ct))
                    await ExecuteAsync(connection, index, ct);
            }
        }

        await transaction.CommitAsync(ct);
        return pending;
    }

    private static async Task<IReadOnlyList<string>> FindPendingAsync(DbConnection connection, CancellationToken ct)
    {
        var pending = new List<string>();
        if (!await TableExistsAsync(connection, "Tickets", ct))
            return pending;

        foreach (var column in Columns)
        {
            if (await TableExistsAsync(connection, column.Table, ct) && !await ColumnExistsAsync(connection, column.Table, column.Column, ct))
                pending.Add($"{column.Table}.{column.Column}");
        }

        if (!await TableExistsAsync(connection, "Periods", ct))
            pending.Add("Periods");
        return pending;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>The context's connection for one step; closed again only if this step had to open it.</summary>
    private sealed class ConnectionScope(DbConnection connection, bool opened) : IAsyncDisposable
    {
        public DbConnection Connection { get; } = connection;

        public static async Task<ConnectionScope> OpenAsync(DbContext db, CancellationToken ct)
        {
            var connection = db.Database.GetDbConnection();
            var opened = connection.State != ConnectionState.Open;
            if (opened)
                await connection.OpenAsync(ct);
            return new ConnectionScope(connection, opened);
        }

        public async ValueTask DisposeAsync()
        {
            if (opened)
                await Connection.CloseAsync();
        }
    }
}