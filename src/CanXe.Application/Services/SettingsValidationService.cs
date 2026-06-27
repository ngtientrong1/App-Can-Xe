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

        if (settings.IsEnabled &&
            !string.IsNullOrWhiteSpace(settings.RtspUrl) &&
            !settings.RtspUrl.Trim().StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
            result.AddError(nameof(CameraDeviceSettingsDto.RtspUrl), "RTSP URL phải bắt đầu bằng rtsp://.");

        if (settings.PhotoRetentionDays is < 1 or > 30)
            result.AddError(nameof(CameraDeviceSettingsDto.PhotoRetentionDays), "Giữ ảnh từ 1 đến 30 ngày.");

        if (settings.SnapshotTimeoutSeconds is < 1 or > 120)
            result.AddError(nameof(CameraDeviceSettingsDto.SnapshotTimeoutSeconds), "Timeout chụp ảnh từ 1 đến 120 giây.");

        return result;
    }

    [GeneratedRegex(@"^[\d\s+\-().]{8,20}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
