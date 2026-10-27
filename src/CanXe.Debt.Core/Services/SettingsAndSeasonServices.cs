using CanXe.Debt.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Services;

/// <summary>Which cargo types count toward customer debt. Only fresh robusta for now; green beans come later.</summary>
public sealed class BilledCargoPolicy(IReadOnlyCollection<string> names)
{
    private readonly HashSet<string> _normalized = names.Select(NameNormalizer.Normalize).Where(n => n.Length > 0).ToHashSet();

    public IReadOnlyCollection<string> Names { get; } = names;

    public bool IsBilled(string? cargo) => _normalized.Contains(NameNormalizer.Normalize(cargo));
}

public sealed class SettingsService(DebtDbContext db)
{
    public const string BilledCargoKey = "billedCargo";
    public static readonly string[] DefaultBilledCargo = ["Rô Tươi"];

    public async Task<BilledCargoPolicy> GetBilledCargoPolicyAsync(CancellationToken ct = default)
    {
        var raw = await db.Settings.AsNoTracking().Where(s => s.Key == BilledCargoKey).Select(s => s.Value).FirstOrDefaultAsync(ct);
        var names = raw is null
            ? DefaultBilledCargo
            : raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new BilledCargoPolicy(names);
    }

    public async Task SaveBilledCargoAsync(IEnumerable<string> names, CancellationToken ct = default)
    {
        var cleaned = names.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var value = string.Join('|', cleaned);
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == BilledCargoKey, ct);
        if (setting is null)
            db.Settings.Add(new AppSetting { Key = BilledCargoKey, Value = value });
        else
            setting.Value = value;
        await db.SaveChangesAsync(ct);
    }

    public const string Require2faKey = "require2fa";

    /// <summary>When on, every account must set up the authenticator app before it can use the app.</summary>
    public async Task<bool> GetRequire2faAsync(CancellationToken ct = default) =>
        await db.Settings.AsNoTracking().AnyAsync(s => s.Key == Require2faKey && s.Value == "1", ct);

    public async Task SaveRequire2faAsync(bool required, CancellationToken ct = default)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == Require2faKey, ct);
        if (setting is null)
            db.Settings.Add(new AppSetting { Key = Require2faKey, Value = required ? "1" : "0" });
        else
            setting.Value = required ? "1" : "0";
        await db.SaveChangesAsync(ct);
    }

    public const string BackupCopyDirectoryKey = "backupCopyDirectory";

    /// <summary>Second folder (USB drive, synced cloud folder…) that every backup is also copied to; null when unset.</summary>
    public async Task<string?> GetBackupCopyDirectoryAsync(CancellationToken ct = default)
    {
        var value = await db.Settings.AsNoTracking().Where(s => s.Key == BackupCopyDirectoryKey).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public async Task SaveBackupCopyDirectoryAsync(string? directory, CancellationToken ct = default)
    {
        var value = directory?.Trim() ?? "";
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == BackupCopyDirectoryKey, ct);
        if (setting is null)
            db.Settings.Add(new AppSetting { Key = BackupCopyDirectoryKey, Value = value });
        else
            setting.Value = value;
        await db.SaveChangesAsync(ct);
    }

    public Task<List<string>> GetKnownCargoNamesAsync(CancellationToken ct = default) =>
        db.Tickets.AsNoTracking().Where(t => t.Cargo != null).Select(t => t.Cargo!).Distinct().OrderBy(c => c).ToListAsync(ct);
}

public sealed class SeasonService(DebtDbContext db)
{
    public Task<List<Season>> GetAllAsync(CancellationToken ct = default) =>
        db.Seasons.AsNoTracking().OrderByDescending(s => s.StartDate).ToListAsync(ct);

    public Task<Season?> GetAsync(int id, CancellationToken ct = default) =>
        db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <summary>The open season, or the most recent one when every season is closed.</summary>
    public async Task<Season?> GetCurrentAsync(CancellationToken ct = default)
    {
        var seasons = await GetAllAsync(ct);
        return seasons.FirstOrDefault(s => !s.IsClosed) ?? seasons.FirstOrDefault();
    }

    public async Task<OperationResult<Season>> CreateAsync(string name, DateOnly start, CancellationToken ct = default)
    {
        name = name.Trim();
        if (name.Length == 0)
            return OperationResult<Season>.Fail("Nhập tên mùa.");
        if (await db.Seasons.AnyAsync(s => !s.IsClosed, ct))
            return OperationResult<Season>.Fail("Đang có một mùa chưa chốt. Hãy chốt mùa đó trước khi mở mùa mới.");

        var latestEnd = (await db.Seasons.Select(s => s.EndDate).ToListAsync(ct)).Max();
        if (latestEnd is { } end && start <= end)
            return OperationResult<Season>.Fail($"Ngày bắt đầu phải sau ngày kết thúc của mùa trước ({end:dd/MM/yyyy}).");

        var season = new Season { Name = name, StartDate = start };
        db.Seasons.Add(season);
        await db.SaveChangesAsync(ct);
        return OperationResult<Season>.Success(season);
    }

    public async Task<OperationResult> CloseAsync(int id, DateOnly end, CancellationToken ct = default)
    {
        var season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (season is null)
            return OperationResult.Fail("Không tìm thấy mùa.");
        if (season.IsClosed)
            return OperationResult.Fail("Mùa này đã chốt rồi.");
        if (end < season.StartDate)
            return OperationResult.Fail("Ngày kết thúc phải sau ngày bắt đầu mùa.");

        season.EndDate = end;
        season.IsClosed = true;
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }

    public async Task<OperationResult> ReopenAsync(int id, CancellationToken ct = default)
    {
        var season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (season is null)
            return OperationResult.Fail("Không tìm thấy mùa.");
        if (!season.IsClosed)
            return OperationResult.Fail("Mùa này chưa chốt.");
        if (await db.Seasons.AnyAsync(s => !s.IsClosed, ct))
            return OperationResult.Fail("Đang có mùa khác chưa chốt nên không mở lại được.");
        if (await db.Seasons.AnyAsync(s => s.Id != id && s.StartDate > season.StartDate, ct))
            return OperationResult.Fail("Chỉ mở lại được mùa gần nhất.");

        season.IsClosed = false;
        season.EndDate = null;
        await db.SaveChangesAsync(ct);
        return OperationResult.Success;
    }
}
