using System.Globalization;
using System.Security.Cryptography;
using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Web;

public static class DebtPaths
{
    /// <summary>Data directory from config (<c>DataDirectory</c>), else %ProgramData%\CanXeDebt.</summary>
    public static string ResolveDataDirectory(IConfiguration configuration)
    {
        var configured = configuration["DataDirectory"];
        return !string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(configured)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CanXeDebt");
    }
}

/// <summary>
/// One-time code that proves whoever creates the first admin account has access to this computer,
/// so a stranger reaching the web page first cannot take it over.
/// </summary>
public sealed class SetupCodeStore(string dataDirectory)
{
    private string? _code;

    public string FilePath { get; } = Path.Combine(dataDirectory, "setup-code.txt");

    public string EnsureCode()
    {
        if (_code is not null)
            return _code;

        _code = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        File.WriteAllText(FilePath, _code);
        return _code;
    }

    public bool Matches(string? attempt) =>
        _code is not null && attempt is not null
        && CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(_code),
            System.Text.Encoding.UTF8.GetBytes(attempt.Trim().ToUpperInvariant()));

    public void Clear()
    {
        _code = null;
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        catch (IOException)
        {
        }
    }
}

public sealed record DatabaseBackupOptions(string DatabasePath, string BackupDirectory)
{
    public int KeepCount { get; init; } = 30;
}

/// <summary>Writes a consistent copy of the database once a day and keeps the newest 30.</summary>
public sealed class DatabaseBackupService(DatabaseBackupOptions options, ILogger<DatabaseBackupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                BackupIfDue();
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void BackupIfDue()
    {
        try
        {
            Directory.CreateDirectory(options.BackupDirectory);
            var today = Path.Combine(options.BackupDirectory, $"debt-{DateTime.Now:yyyyMMdd}.db");
            if (File.Exists(today) || !File.Exists(options.DatabasePath))
                return;

            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={options.DatabasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"VACUUM INTO '{today.Replace("'", "''")}'";
                command.ExecuteNonQuery();
            }

            foreach (var old in Directory.GetFiles(options.BackupDirectory, "debt-*.db")
                         .OrderByDescending(f => f).Skip(options.KeepCount))
                File.Delete(old);
            logger.LogInformation("Đã sao lưu dữ liệu: {File}", today);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sao lưu dữ liệu thất bại");
        }
    }
}

public static class Fmt
{
    private static readonly NumberFormatInfo Vi = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3],
        NegativeSign = "-"
    };

    public static string Vnd(long value) => value.ToString("N0", Vi);

    public static string Vnd(long? value) => value is { } v ? Vnd(v) : "—";

    /// <summary>A balance for a ledger column: "còn nợ" as a number, a customer who paid ahead as "… (ứng trước)".</summary>
    public static string Balance(long balance) => balance < 0 ? $"{Vnd(-balance)} (ứng trước)" : Vnd(balance);

    public static string Kg(decimal? value) => value is { } v ? v.ToString("N0", Vi) : "—";

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Date(DateOnly? date) => date is { } d ? Date(d) : "—";

    public static string DateTime(DateTimeOffset value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string UnitPrice(TicketRecord ticket) =>
        ticket.UnitPrice is not { } price ? "—"
        : ticket.HasPriceSplit && ticket.SecondUnitPrice is { } second ? $"{Kg(price)} / {Kg(second)}"
        : Kg(price);

    /// <summary>Reads "1.500.000", "1,500,000" or "1500000"; anything else is rejected.</summary>
    public static bool TryParseVnd(string? text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        var allowed = text.All(c => char.IsDigit(c) || c is '.' or ',' or ' ');
        return allowed && digits.Length is > 0 and <= 15 && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public static string Method(PaymentMethod method) => method == PaymentMethod.Cash ? "Tiền mặt" : "Chuyển khoản";
}
