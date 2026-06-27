using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
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
        Handshake = entity.Handshake
    };
}

public sealed class CameraDeviceSettingsRepository(CanXeDbContext db, ISecretProtector secretProtector)
    : ICameraDeviceSettingsRepository
{
    public async Task<CameraDeviceSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await db.CameraDeviceSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
            return null;

        return new CameraDeviceSettingsDto
        {
            CameraName = entity.CameraName,
            IsEnabled = entity.IsEnabled,
            RtspUrl = entity.RtspUrl,
            Username = entity.Username,
            Password = string.IsNullOrWhiteSpace(entity.ProtectedPassword)
                ? null
                : secretProtector.Unprotect(entity.ProtectedPassword),
            PreviewEnabled = entity.PreviewEnabled,
            AutoConnectionCheck = entity.AutoConnectionCheck,
            SnapshotTimeoutSeconds = entity.SnapshotTimeoutSeconds,
            PhotoRetentionDays = entity.PhotoRetentionDays
        };
    }

    public async Task SaveAsync(CameraDeviceSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var entity = await db.CameraDeviceSettings.FirstOrDefaultAsync(cancellationToken)
                     ?? db.CameraDeviceSettings.Add(new CameraDeviceSettings()).Entity;

        entity.CameraName = settings.CameraName?.Trim();
        entity.IsEnabled = settings.IsEnabled;
        entity.RtspUrl = settings.RtspUrl?.Trim();
        entity.Username = settings.Username?.Trim();

        if (!string.IsNullOrEmpty(settings.Password))
            entity.ProtectedPassword = secretProtector.Protect(settings.Password);

        entity.PreviewEnabled = settings.PreviewEnabled;
        entity.AutoConnectionCheck = settings.AutoConnectionCheck;
        entity.SnapshotTimeoutSeconds = settings.SnapshotTimeoutSeconds;
        entity.PhotoRetentionDays = settings.PhotoRetentionDays;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetProtectedPasswordAsync(CancellationToken cancellationToken = default)
    {
        var entity = await db.CameraDeviceSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return entity?.ProtectedPassword;
    }
}
