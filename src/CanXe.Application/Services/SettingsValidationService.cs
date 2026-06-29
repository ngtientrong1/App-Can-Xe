using System.Text.RegularExpressions;
using CanXe.Application.Models;

namespace CanXe.Application.Services;

public static partial class SettingsValidationService
{
    public static SettingsValidationResult ValidateStation(StationSettingsDto settings)
    {
        var result = new SettingsValidationResult();
        if (string.IsNullOrWhiteSpace(settings.StationName))
            result.AddError(nameof(StationSettingsDto.StationName), "Tên trạm cân là bắt buộc.");

        if (!string.IsNullOrWhiteSpace(settings.Phone) && !PhoneRegex().IsMatch(settings.Phone.Trim()))
            result.AddError(nameof(StationSettingsDto.Phone), "Số điện thoại không hợp lệ.");

        if (!string.IsNullOrWhiteSpace(settings.Email) && !EmailRegex().IsMatch(settings.Email.Trim()))
            result.AddError(nameof(StationSettingsDto.Email), "Email không hợp lệ.");

        return result;
    }

    public static SettingsValidationResult ValidateCamera(CameraDeviceSettingsDto settings)
    {
        var result = new SettingsValidationResult();
        var normalized = CameraSettingsMapper.NormalizeLoaded(settings);

        if (settings.IsEnabled)
        {
            if (string.IsNullOrWhiteSpace(normalized.RtspHost))
                result.AddError(nameof(CameraDeviceSettingsDto.RtspHost), "Host/IP camera là bắt buộc.");

            if (normalized.RtspPort is < 1 or > 65535)
                result.AddError(nameof(CameraDeviceSettingsDto.RtspPort), "Port RTSP phải từ 1 đến 65535.");
        }

        if (settings.PhotoRetentionDays is < 1 or > 30)
            result.AddError(nameof(CameraDeviceSettingsDto.PhotoRetentionDays), "Giữ ảnh từ 1 đến 30 ngày.");

        if (settings.ConnectTimeoutSeconds is < 1 or > 120)
            result.AddError(nameof(CameraDeviceSettingsDto.ConnectTimeoutSeconds), "Timeout kết nối từ 1 đến 120 giây.");

        if (settings.SnapshotTimeoutSeconds is < 1 or > 120)
            result.AddError(nameof(CameraDeviceSettingsDto.SnapshotTimeoutSeconds), "Timeout chụp ảnh từ 1 đến 120 giây.");

        return result;
    }

    [GeneratedRegex(@"^[\d\s+\-().]{8,20}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
