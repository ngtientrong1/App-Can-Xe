using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Infrastructure.Repositories;

public sealed class StationSettingsRepository(CanXeDbContext db) : IStationSettingsRepository
{
    public async Task<StationSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await db.StationSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task SaveAsync(StationSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var entity = await db.StationSettings.FirstOrDefaultAsync(cancellationToken)
                     ?? db.StationSettings.Add(new StationSettings()).Entity;

        entity.StationName = settings.StationName.Trim();
        entity.OwnerName = settings.OwnerName?.Trim();
        entity.Address = settings.Address?.Trim();
        entity.Phone = settings.Phone?.Trim();
        entity.Email = settings.Email?.Trim();
        entity.TaxCode = settings.TaxCode?.Trim();
        entity.LogoPath = settings.LogoPath?.Trim();
        entity.TicketFooterText = settings.TicketFooterText?.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static StationSettingsDto Map(StationSettings entity) => new()
    {
        StationName = entity.StationName,
        OwnerName = entity.OwnerName,
        Address = entity.Address,
        Phone = entity.Phone,
        Email = entity.Email,
        TaxCode = entity.TaxCode,
        LogoPath = entity.LogoPath,
        TicketFooterText = entity.TicketFooterText
    };
}

public sealed class ScaleDeviceSettingsRepository(CanXeDbContext db) : IScaleDeviceSettingsRepository
{
    public async Task<ScaleDeviceSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await db.ScaleDeviceSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task SaveAsync(ScaleDeviceSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var entity = await db.ScaleDeviceSettings.FirstOrDefaultAsync(cancellationToken)
                     ?? db.ScaleDeviceSettings.Add(new ScaleDeviceSettings()).Entity;

        entity.DeviceMode = settings.DeviceMode.Trim();
        entity.PortName = settings.PortName.Trim();
        entity.BaudRate = settings.BaudRate;
        entity.DataBits = settings.DataBits;
        entity.Parity = settings.Parity.Trim();
        entity.StopBits = settings.StopBits.Trim();
        entity.Handshake = settings.Handshake.Trim();
        entity.ScaleInputMode = settings.ScaleInputMode?.ToString();
        entity.AutoConnectScaleOnStartup = settings.AutoConnectScaleOnStartup;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ScaleDeviceSettingsDto Map(ScaleDeviceSettings entity) => new()
    {
        DeviceMode = entity.DeviceMode,
        PortName = entity.PortName,
        BaudRate = entity.BaudRate,
        DataBits = entity.DataBits,
        Parity = entity.Parity,
        StopBits = entity.StopBits,
        Handshake = entity.Handshake,
        ScaleInputMode = ParseScaleInputMode(entity.ScaleInputMode),
        AutoConnectScaleOnStartup = entity.AutoConnectScaleOnStartup
    };

    private static ScaleInputMode? ParseScaleInputMode(string? value) =>
        Enum.TryParse<ScaleInputMode>(value, true, out var parsed) ? parsed : null;
}

public sealed class CameraDeviceSettingsRepository(CanXeDbContext db, ISecretProtector secretProtector)
    : ICameraDeviceSettingsRepository
{
    public async Task<CameraDeviceSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await db.CameraDeviceSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
            return null;

        return CameraSettingsMapper.NormalizeLoaded(MapForEdit(entity));
    }

    public async Task<CameraRuntimeSettings?> GetRuntimeAsync(CancellationToken cancellationToken = default)
    {
        var entity = await db.CameraDeviceSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
            return null;

        var dto = CameraSettingsMapper.NormalizeLoaded(MapForEdit(entity));
        var password = string.IsNullOrWhiteSpace(entity.ProtectedPassword)
            ? null
            : secretProtector.Unprotect(entity.ProtectedPassword);

        return CameraSettingsMapper.ToRuntime(dto, password);
    }

    public async Task SaveAsync(CameraDeviceSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var entity = await db.CameraDeviceSettings.FirstOrDefaultAsync(cancellationToken)
                     ?? db.CameraDeviceSettings.Add(new CameraDeviceSettings()).Entity;

        var normalized = CameraSettingsMapper.NormalizeLoaded(settings);
        entity.CameraName = normalized.CameraName?.Trim();
        entity.IsEnabled = normalized.IsEnabled;
        entity.RtspHost = normalized.RtspHost?.Trim();
        entity.RtspPort = normalized.RtspPort > 0 ? normalized.RtspPort : RtspUrlBuilder.DefaultPort;
        entity.RtspPath = string.IsNullOrWhiteSpace(normalized.RtspPath) ? "/" : normalized.RtspPath.Trim();
        entity.RtspTransport = string.IsNullOrWhiteSpace(normalized.RtspTransport)
            ? RtspUrlBuilder.DefaultTransport
            : normalized.RtspTransport.Trim();
        entity.Username = normalized.Username?.Trim();
        entity.RtspUrl = RtspUrlBuilder.Build(
            entity.RtspHost,
            entity.RtspPort,
            entity.RtspPath,
            entity.Username,
            password: null,
            entity.RtspTransport);

        if (settings.ClearStoredPassword)
            entity.ProtectedPassword = null;
        else if (!string.IsNullOrEmpty(settings.Password))
            entity.ProtectedPassword = secretProtector.Protect(settings.Password);

        entity.PreviewEnabled = normalized.PreviewEnabled;
        entity.AutoConnectionCheck = normalized.AutoConnectCameraOnStartup;
        entity.ConnectTimeoutSeconds = normalized.ConnectTimeoutSeconds;
        entity.SnapshotTimeoutSeconds = normalized.SnapshotTimeoutSeconds;
        entity.PhotoRetentionDays = normalized.PhotoRetentionDays;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static CameraDeviceSettingsDto MapForEdit(CameraDeviceSettings entity) =>
        new()
        {
            CameraName = entity.CameraName,
            IsEnabled = entity.IsEnabled,
            RtspHost = entity.RtspHost,
            RtspPort = entity.RtspPort > 0 ? entity.RtspPort : RtspUrlBuilder.DefaultPort,
            RtspPath = entity.RtspPath,
            RtspUrl = entity.RtspUrl,
            Username = entity.Username,
            HasStoredPassword = !string.IsNullOrWhiteSpace(entity.ProtectedPassword),
            RtspTransport = string.IsNullOrWhiteSpace(entity.RtspTransport)
                ? RtspUrlBuilder.DefaultTransport
                : entity.RtspTransport,
            PreviewEnabled = entity.PreviewEnabled,
            AutoConnectCameraOnStartup = entity.AutoConnectionCheck,
            ConnectTimeoutSeconds = entity.ConnectTimeoutSeconds > 0 ? entity.ConnectTimeoutSeconds : 5,
            SnapshotTimeoutSeconds = entity.SnapshotTimeoutSeconds,
            PhotoRetentionDays = entity.PhotoRetentionDays
        };
}
