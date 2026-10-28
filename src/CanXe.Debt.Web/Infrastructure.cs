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

/// <summary>Appends unhandled errors to a text file the owner can send over; keeps the file from growing without end.</summary>
public sealed class FileErrorLog(string path)
{
    private const long MaxBytes = 2_000_000;
    private readonly object _gate = new();

    public string Path { get; } = path;

    public void Write(string context, Exception exception)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                if (File.Exists(Path) && new FileInfo(Path).Length > MaxBytes)
                    File.Move(Path, Path + ".old", overwrite: true);
                File.AppendAllText(Path, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Binds dates typed day/month/year, so every date box reads the same whatever the browser's language is.</summary>
public sealed class DateOnlyModelBinderProvider : Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinderProvider
{
    public Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder? GetBinder(Microsoft.AspNetCore.Mvc.ModelBinding.ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(DateOnly) ? new DateOnlyModelBinder(context.Metadata.IsNullableValueType) : null;
}

public sealed class DateOnlyModelBinder(bool nullable) : Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder
{
    public Task BindModelAsync(Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult.None)
            return Task.CompletedTask;

        context.ModelState.SetModelValue(context.ModelName, value);
        var text = value.FirstValue?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (nullable)
                context.Result = Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult.Success(null);
        }
        else if (Fmt.TryParseDate(text, out var date))
        {
            context.Result = Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult.Success(date);
        }
        else
        {
            context.ModelState.TryAddModelError(context.ModelName, "Ngày không hợp lệ. Nhập dạng 27/10/2026.");
        }

        return Task.CompletedTask;
    }
}

/// <summary>What the period card needs besides the period itself.</summary>
public sealed record PeriodCardModel(
    PeriodView View,
    int CustomerId,
    int SeasonId,
    bool SeasonOpen,
    bool CanUndo,
    IReadOnlyDictionary<string, string> Stations)
{
    public string StationName(string code) => Stations.GetValueOrDefault(code, code);
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

    /// <summary>Colour class that tells scale stations apart at a glance; the same station always gets the same one.</summary>
    public static string StationClass(string code) => "st st-" + (code.ToUpperInvariant().Sum(c => c) % 4);

    public static string Range(DateOnly from, DateOnly? to) =>
        to is null ? $"từ {from:dd/MM/yyyy}"
        : from == to ? Date(from)
        : $"{from:dd/MM} – {to:dd/MM/yyyy}";

    /// <summary>"24/11/2025 · phiếu 218/11 → 225/11 (3 xe)" or, across days, "24/11/2025 (phiếu 218/11) → 25/11/2025 (phiếu 239/11) · 5 xe".</summary>
    public static string PeriodRange(PeriodView period)
    {
        if (period.FirstTicket is not { } first || period.LastTicket is not { } last)
            return "(không có xe)";

        var count = period.TruckCount;
        return first.TicketDate == last.TicketDate
            ? $"{Date(first.TicketDate)} · phiếu {first.DisplayNumber}{(count > 1 ? $" → {last.DisplayNumber}" : "")} ({count} xe)"
            : $"{Date(first.TicketDate)} (phiếu {first.DisplayNumber}) → {Date(last.TicketDate)} (phiếu {last.DisplayNumber}) · {count} xe";
    }

    /// <summary>"Lô 22–23/10" for three trucks or more; one or two trucks are shown by day and ticket ("ngày 27/10 phiếu 57/10").</summary>
    public static string LotLabel(PeriodView period) =>
        period.IsOpen ? "Chưa gom lô" : LotNaming.Name(period.Tickets.Select(t => (t.TicketDate, t.DisplayNumber)).ToList());
    public static string AdjustTitle(TicketRecord t) =>
        $"Phiếu cân gốc: {Kg(t.OriginalNetKg)} kg" + (string.IsNullOrWhiteSpace(t.AdjustReason) ? "" : $" · {t.AdjustReason}")
        + (t.AdjustedBy is null ? "" : $" · {t.AdjustedBy} chỉnh");

    public static string StageText(TicketStage stage) => stage switch
    {
        TicketStage.AwaitingSecondWeigh => "Chờ cân lần 2",
        TicketStage.NoPrice => "Chưa có giá",
        _ => "Đã tính tiền"
    };

    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.Offset => "Bù trừ",
        _ => "Chuyển khoản"
    };

    private static readonly string[] DateFormats =
        ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy", "ddMMyyyy", "yyyy-MM-dd"];

    /// <summary>Reads a date typed as day/month/year (27/10/2026, 27-10-2026, 27102026) or ISO yyyy-MM-dd.</summary>
    public static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Whole days from <paramref name="from"/> to today, never negative.</summary>
    public static int DaysSince(DateOnly from) => Math.Max(0, DateOnly.FromDateTime(System.DateTime.Today).DayNumber - from.DayNumber);
}
