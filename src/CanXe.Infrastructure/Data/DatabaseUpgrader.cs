using CanXe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Infrastructure.Data;

public static class DatabaseUpgrader
{
    public const string Phase16MigrationId = "202606270001_Phase16_AuditAndWeightOverride";
    public const string Phase17MigrationId = "202606270002_Phase17_StationAndDeviceSettings";
    public const string Phase2CMigrationId = "202606270003_Phase2C_ScaleInputMode";
    public const string Phase2DMigrationId = "202606270004_Phase2D_AutoConnectScale";
    public const string Phase3AMigrationId = "202606270005_Phase3A_CameraRtspAutoConnect";
    public const string Phase4MigrationId = "202606290001_Phase4_PrintSettingsAndStationFields";
    public const string Phase4Rc11MigrationId = "202606300001_Phase4_Rc11_PrintRenderingMode";
    public const string Phase4Rc18MigrationId = "202606300002_Phase4_Rc18_PrintLayoutMode";
    public const string Phase4Rc23MigrationId = "202606300003_Phase4_Rc23_A4TwoUpAndSoftDelete";
    public const string Phase5Rc6MigrationId = "202607040006_Phase5_Rc6_CatalogFields";

    public static async Task UpgradeAsync(CanXeDbContext db, string databasePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(databasePath))
            BackupDatabase(databasePath);

        if (!await TableExistsAsync(db, "WeighTickets", cancellationToken))
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await RecordMigrationAsync(db, Phase16MigrationId, cancellationToken);
            await RecordMigrationAsync(db, Phase17MigrationId, cancellationToken);
            await RecordMigrationAsync(db, Phase2CMigrationId, cancellationToken);
            await RecordMigrationAsync(db, Phase2DMigrationId, cancellationToken);
            await ApplyPhase3ACameraRtspFieldsAsync(db, cancellationToken);
            await RecordMigrationAsync(db, Phase3AMigrationId, cancellationToken);
            await ApplyPhase4PrintAndStationFieldsAsync(db, cancellationToken);
            await RecordMigrationAsync(db, Phase4MigrationId, cancellationToken);
            await ApplyPhase4Rc11PrintRenderingModeAsync(db, cancellationToken);
            await RecordMigrationAsync(db, Phase4Rc11MigrationId, cancellationToken);
            await ApplyPhase4Rc18PrintLayoutModeAsync(db, cancellationToken);
            await RecordMigrationAsync(db, Phase4Rc18MigrationId, cancellationToken);
            await ApplyPhase4Rc23A4TwoUpAndSoftDeleteAsync(db, cancellationToken);
            await RecordMigrationAsync(db, Phase4Rc23MigrationId, cancellationToken);
            await ApplyPhase5Rc6CatalogFieldsAsync(db, cancellationToken);
            await RecordMigrationAsync(db, Phase5Rc6MigrationId, cancellationToken);
            return;
        }

        await ApplyPhase16WeighEventColumnsAsync(db, cancellationToken);
        await ApplyPhase16AuditLogsAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase16MigrationId, cancellationToken);

        await ApplyPhase17SettingsTablesAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase17MigrationId, cancellationToken);

        await ApplyPhase2CScaleInputModeAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase2CMigrationId, cancellationToken);

        await ApplyPhase2DAutoConnectScaleAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase2DMigrationId, cancellationToken);

        await ApplyPhase3ACameraRtspFieldsAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase3AMigrationId, cancellationToken);

        await ApplyPhase4PrintAndStationFieldsAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase4MigrationId, cancellationToken);
        await ApplyPhase4Rc11PrintRenderingModeAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase4Rc11MigrationId, cancellationToken);
        await ApplyPhase4Rc18PrintLayoutModeAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase4Rc18MigrationId, cancellationToken);
        await ApplyPhase4Rc23A4TwoUpAndSoftDeleteAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase4Rc23MigrationId, cancellationToken);
        await ApplyPhase5Rc6CatalogFieldsAsync(db, cancellationToken);
        await RecordMigrationAsync(db, Phase5Rc6MigrationId, cancellationToken);
    }

    public static void BackupDatabase(string databasePath)
    {
        var backupDir = Path.Combine(Path.GetDirectoryName(databasePath)!, "backups");
        Directory.CreateDirectory(backupDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"{Path.GetFileNameWithoutExtension(databasePath)}_{stamp}.db");
        File.Copy(databasePath, backupPath, overwrite: true);
    }

    private static async Task ApplyPhase17SettingsTablesAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, "StationSettings", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE StationSettings (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    StationName TEXT NOT NULL,
                    OwnerName TEXT NULL,
                    Address TEXT NULL,
                    Phone TEXT NULL,
                    Email TEXT NULL,
                    TaxCode TEXT NULL,
                    LogoPath TEXT NULL,
                    TicketFooterText TEXT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                INSERT INTO StationSettings (Id, StationName, UpdatedAt)
                VALUES (1, 'Trạm cân CanXe', datetime('now'));
                """,
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "ScaleDeviceSettings", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE ScaleDeviceSettings (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    DeviceMode TEXT NOT NULL DEFAULT 'Simulation',
                    PortName TEXT NOT NULL DEFAULT 'COM1',
                    BaudRate INTEGER NOT NULL DEFAULT 9600,
                    DataBits INTEGER NOT NULL DEFAULT 8,
                    Parity TEXT NOT NULL DEFAULT 'None',
                    StopBits TEXT NOT NULL DEFAULT 'One',
                    Handshake TEXT NOT NULL DEFAULT 'None',
                    UpdatedAt TEXT NOT NULL
                );
                INSERT INTO ScaleDeviceSettings (Id, DeviceMode, PortName, BaudRate, DataBits, Parity, StopBits, Handshake, UpdatedAt)
                VALUES (1, 'Simulation', 'COM1', 9600, 8, 'None', 'One', 'None', datetime('now'));
                """,
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "CameraDeviceSettings", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE CameraDeviceSettings (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    CameraName TEXT NULL,
                    IsEnabled INTEGER NOT NULL DEFAULT 1,
                    RtspUrl TEXT NULL,
                    Username TEXT NULL,
                    ProtectedPassword TEXT NULL,
                    PreviewEnabled INTEGER NOT NULL DEFAULT 1,
                    AutoConnectionCheck INTEGER NOT NULL DEFAULT 1,
                    SnapshotTimeoutSeconds INTEGER NOT NULL DEFAULT 5,
                    PhotoRetentionDays INTEGER NOT NULL DEFAULT 3,
                    UpdatedAt TEXT NOT NULL
                );
                INSERT INTO CameraDeviceSettings (Id, IsEnabled, PreviewEnabled, AutoConnectionCheck, SnapshotTimeoutSeconds, PhotoRetentionDays, UpdatedAt)
                VALUES (1, 1, 1, 1, 5, 3, datetime('now'));
                """,
                cancellationToken);
        }
    }

    private static async Task ApplyPhase2CScaleInputModeAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, "ScaleDeviceSettings", cancellationToken))
            return;

        await AddColumnIfMissingAsync(db, "ScaleDeviceSettings", "ScaleInputMode", "TEXT NULL", cancellationToken);
    }

    private static async Task ApplyPhase3ACameraRtspFieldsAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, "CameraDeviceSettings", cancellationToken))
            return;

        await AddColumnIfMissingAsync(db, "CameraDeviceSettings", "RtspHost", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "CameraDeviceSettings", "RtspPort", "INTEGER NOT NULL DEFAULT 554", cancellationToken);
        await AddColumnIfMissingAsync(db, "CameraDeviceSettings", "RtspPath", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "CameraDeviceSettings", "RtspTransport", "TEXT NOT NULL DEFAULT 'TCP'", cancellationToken);
        await AddColumnIfMissingAsync(db, "CameraDeviceSettings", "ConnectTimeoutSeconds", "INTEGER NOT NULL DEFAULT 5", cancellationToken);
    }

    private static async Task ApplyPhase2DAutoConnectScaleAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, "ScaleDeviceSettings", cancellationToken))
            return;

        await AddColumnIfMissingAsync(
            db,
            "ScaleDeviceSettings",
            "AutoConnectScaleOnStartup",
            "INTEGER NOT NULL DEFAULT 1",
            cancellationToken);
    }

    private static async Task ApplyPhase16WeighEventColumnsAsync(
        CanXeDbContext db,
        CancellationToken cancellationToken)
    {
        await AddColumnIfMissingAsync(db, "WeighEvents", "OverrideWeightGrams", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "WeighEvents", "IsManualOverride", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, "WeighEvents", "OverrideReason", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "WeighEvents", "OverrideAt", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "WeighEvents", "OverrideBy", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "WeighEvents", "RawScaleData", "TEXT NULL", cancellationToken);
    }

    private static async Task ApplyPhase16AuditLogsAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(db, "AuditLogs", cancellationToken))
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE AuditLogs (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                TicketId INTEGER NOT NULL,
                FieldName TEXT NOT NULL,
                OldValue TEXT NULL,
                NewValue TEXT NULL,
                Reason TEXT NULL,
                EditedAt TEXT NOT NULL,
                EditedBy TEXT NULL,
                IsDeveloperOverride INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IX_AuditLogs_TicketId ON AuditLogs (TicketId);
            CREATE INDEX IX_AuditLogs_EditedAt ON AuditLogs (EditedAt DESC);
            """,
            cancellationToken);
    }

    private static async Task AddColumnIfMissingAsync(
        CanXeDbContext db,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(db, table, column, cancellationToken))
            return;

        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE {table} ADD COLUMN {column} {definition};", cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        CanXeDbContext db,
        string table,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = table;
            command.Parameters.Add(parameter);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result) > 0;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        CanXeDbContext db,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({table});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task ApplyPhase4PrintAndStationFieldsAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(db, "StationSettings", "StationSubtitle", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE StationSettings ADD COLUMN StationSubtitle TEXT NULL;",
                cancellationToken);
        }

        if (!await ColumnExistsAsync(db, "StationSettings", "SignLocationName", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE StationSettings ADD COLUMN SignLocationName TEXT NULL;",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "PrintSettings", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE PrintSettings (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    PreferredPrinterName TEXT NULL,
                    PaperSize TEXT NOT NULL,
                    PaperOrientation TEXT NOT NULL,
                    DefaultCopies INTEGER NOT NULL,
                    TopCopyLabel TEXT NOT NULL,
                    BottomCopyLabel TEXT NOT NULL,
                    ShowLogo INTEGER NOT NULL,
                    ShowPrice INTEGER NOT NULL,
                    TicketFooterText TEXT NULL,
                    SignLocationName TEXT NULL,
                    ShowReprintWatermark INTEGER NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                INSERT INTO PrintSettings (Id, PaperSize, PaperOrientation, DefaultCopies, TopCopyLabel, BottomCopyLabel, ShowLogo, ShowPrice, ShowReprintWatermark, UpdatedAt)
                VALUES (1, 'A4', 'Portrait', 1, 'LIÊN TRẠM CÂN', 'LIÊN KHÁCH HÀNG', 1, 1, 1, datetime('now'));
                """,
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "PrintJobHistories", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE PrintJobHistories (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    TicketId INTEGER NULL,
                    TicketNumber TEXT NULL,
                    PrinterName TEXT NULL,
                    PaperSize TEXT NOT NULL,
                    Copies INTEGER NOT NULL,
                    RequestedAt TEXT NOT NULL,
                    CompletedAt TEXT NULL,
                    Status INTEGER NOT NULL,
                    ErrorMessage TEXT NULL,
                    IsReprint INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_PrintJobHistories_TicketId ON PrintJobHistories (TicketId);
                CREATE INDEX IF NOT EXISTS IX_PrintJobHistories_RequestedAt ON PrintJobHistories (RequestedAt);
                """,
                cancellationToken);
        }
    }

    private static async Task ApplyPhase4Rc11PrintRenderingModeAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(db, "PrintSettings", cancellationToken)
            && !await ColumnExistsAsync(db, "PrintSettings", "PrintRenderingMode", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE PrintSettings ADD COLUMN PrintRenderingMode TEXT NOT NULL DEFAULT 'RasterCompatibility';
                UPDATE PrintSettings SET PrintRenderingMode = 'RasterCompatibility' WHERE PrintRenderingMode IS NULL OR PrintRenderingMode = '';
                """,
                cancellationToken);
        }
    }

    private static async Task ApplyPhase4Rc18PrintLayoutModeAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(db, "PrintSettings", cancellationToken)
            && !await ColumnExistsAsync(db, "PrintSettings", "PrintLayoutMode", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE PrintSettings ADD COLUMN PrintLayoutMode TEXT NOT NULL DEFAULT 'A5SingleTicket';
                UPDATE PrintSettings SET PrintLayoutMode = 'A5SingleTicket' WHERE PrintLayoutMode IS NULL OR PrintLayoutMode = '';
                """,
                cancellationToken);
        }
    }

    private static async Task ApplyPhase4Rc23A4TwoUpAndSoftDeleteAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(db, "WeighTickets", cancellationToken))
        {
            await AddColumnIfMissingAsync(db, "WeighTickets", "IsDeleted", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddColumnIfMissingAsync(db, "WeighTickets", "DeletedAt", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "WeighTickets", "DeletedBy", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "WeighTickets", "DeleteReason", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS IX_WeighTickets_IsDeleted ON WeighTickets (IsDeleted);",
                cancellationToken);
        }

        if (await TableExistsAsync(db, "PrintSettings", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE PrintSettings
                SET PrintLayoutMode = 'A4TwoUp'
                WHERE PrintLayoutMode IS NULL
                   OR PrintLayoutMode = ''
                   OR PrintLayoutMode = 'A5SingleTicket';
                """,
                cancellationToken);
        }
    }

    private static async Task ApplyPhase5Rc6CatalogFieldsAsync(CanXeDbContext db, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(db, "Customers", cancellationToken))
        {
            await AddColumnIfMissingAsync(db, "Customers", "Phone", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "Customers", "Address", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "Customers", "Note", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "Customers", "IsDeleted", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddColumnIfMissingAsync(db, "Customers", "CreatedAt", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(db, "Customers", "UpdatedAt", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "Customers", "DeletedAt", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE Customers SET CreatedAt = COALESCE(datetime(LastUsedAt), datetime('now'))
                WHERE CreatedAt = '' OR CreatedAt IS NULL;
                """,
                cancellationToken);
        }

        if (await TableExistsAsync(db, "Vehicles", cancellationToken))
        {
            await AddColumnIfMissingAsync(db, "Vehicles", "OwnerName", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "Vehicles", "Note", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "Vehicles", "IsActive", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await AddColumnIfMissingAsync(db, "Vehicles", "IsDeleted", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddColumnIfMissingAsync(db, "Vehicles", "CreatedAt", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(db, "Vehicles", "UpdatedAt", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "Vehicles", "DeletedAt", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE Vehicles SET CreatedAt = COALESCE(datetime(LastUsedAt), datetime('now'))
                WHERE CreatedAt = '' OR CreatedAt IS NULL;
                """,
                cancellationToken);
        }

        if (await TableExistsAsync(db, "CargoTypes", cancellationToken))
        {
            await AddColumnIfMissingAsync(db, "CargoTypes", "DefaultUnitPriceVndPerKg", "INTEGER NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "CargoTypes", "Unit", "TEXT NOT NULL DEFAULT 'kg'", cancellationToken);
            await AddColumnIfMissingAsync(db, "CargoTypes", "Note", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "CargoTypes", "LastUsedAt", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "CargoTypes", "IsDeleted", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await AddColumnIfMissingAsync(db, "CargoTypes", "CreatedAt", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(db, "CargoTypes", "UpdatedAt", "TEXT NULL", cancellationToken);
            await AddColumnIfMissingAsync(db, "CargoTypes", "DeletedAt", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE CargoTypes SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                UPDATE CargoTypes SET Unit = 'kg' WHERE Unit IS NULL OR Unit = '';
                """,
                cancellationToken);
        }
    }

    private static async Task RecordMigrationAsync(
        CanXeDbContext db,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
                MigrationId TEXT NOT NULL PRIMARY KEY,
                ProductVersion TEXT NOT NULL
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES ({0}, '10.0.9');
            """,
            [migrationId],
            cancellationToken);
    }
}
