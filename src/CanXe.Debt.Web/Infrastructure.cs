using System.Globalization;
using System.Security.Cryptography;
using CanXe.Debt.Data;
using CanXe.Debt.Services;
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

/// <summary>Makes sure there is one automatic backup per day, checking every hour (the PC may be off at night).</summary>
public sealed class DailyBackupService(BackupManager backups, IServiceScopeFactory scopes, ILogger<DailyBackupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await BackupIfDueAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task BackupIfDueAsync(CancellationToken ct)
    {
        if (backups.DailyExists(DateOnly.FromDateTime(DateTime.Now)))
            return;

        string? copyDirectory;
        using (var scope = scopes.CreateScope())
            copyDirectory = await scope.ServiceProvider.GetRequiredService<SettingsService>().GetBackupCopyDirectoryAsync(ct);

        var result = backups.Create(BackupManager.Daily, copyDirectory);
        if (!result.Ok)
            logger.LogError("Sao lưu tự động thất bại: {Error}", result.Error);
        else if (result.CopyError is not null)
            logger.LogWarning("Đã sao lưu nhưng không chép được sang thư mục phụ: {Error}", result.CopyError);
        else
            logger.LogInformation("Đã sao lưu dữ liệu: {File}", result.Info!.FileName);
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
