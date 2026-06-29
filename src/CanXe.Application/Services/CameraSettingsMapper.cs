using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public static class CameraSettingsMapper
{
    public static CameraDeviceSettingsDto NormalizeLoaded(CameraDeviceSettingsDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.RtspHost))
            return dto;

        if (string.IsNullOrWhiteSpace(dto.RtspUrl))
            return dto;

        if (!RtspUrlBuilder.TryParse(dto.RtspUrl, out var endpoint))
            return dto;

        dto.RtspHost = endpoint.Host;
        dto.RtspPort = endpoint.Port;
        dto.RtspPath = endpoint.Path;
        dto.Username ??= endpoint.Username;
        dto.RtspTransport = endpoint.Transport;
        return dto;
    }

    public static CameraRuntimeSettings ToRuntime(CameraDeviceSettingsDto dto, string? decryptedPassword) =>
        new()
        {
            IsEnabled = dto.IsEnabled,
            AutoConnectCameraOnStartup = dto.AutoConnectCameraOnStartup,
            CameraName = dto.CameraName,
            RtspHost = dto.RtspHost,
            RtspPort = dto.RtspPort > 0 ? dto.RtspPort : RtspDefaults.DefaultPort,
            RtspPath = dto.RtspPath,
            Username = dto.Username,
            Password = decryptedPassword,
            RtspTransport = string.IsNullOrWhiteSpace(dto.RtspTransport) ? RtspDefaults.DefaultTransport : dto.RtspTransport,
            ConnectTimeoutSeconds = dto.ConnectTimeoutSeconds,
            SnapshotTimeoutSeconds = dto.SnapshotTimeoutSeconds,
            PhotoRetentionDays = dto.PhotoRetentionDays,
            PreviewEnabled = dto.PreviewEnabled
        };

    public static CameraDeviceSettingsDto FromRuntime(CameraRuntimeSettings runtime) =>
        new()
        {
            CameraName = runtime.CameraName,
            IsEnabled = runtime.IsEnabled,
            RtspHost = runtime.RtspHost,
            RtspPort = runtime.RtspPort,
            RtspPath = runtime.RtspPath,
            Username = runtime.Username,
            Password = runtime.Password,
            RtspTransport = runtime.RtspTransport,
            ConnectTimeoutSeconds = runtime.ConnectTimeoutSeconds,
            SnapshotTimeoutSeconds = runtime.SnapshotTimeoutSeconds,
            PhotoRetentionDays = runtime.PhotoRetentionDays,
            PreviewEnabled = runtime.PreviewEnabled,
            AutoConnectCameraOnStartup = runtime.AutoConnectCameraOnStartup
        };
}
