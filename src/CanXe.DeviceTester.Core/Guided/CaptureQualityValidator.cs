using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Guided;

public static class CaptureQualityValidator
{
    private const double MinStableDurationSeconds = GuidedCaptureTimings.StableMinimumValidDurationSeconds;
    private const double MaxNoDataGapWarningMs = 2000;

    public static CaptureQualityResult Validate(CaptureRecordingStats stats)
    {
        var messages = new List<string>();
        var invalid = false;
        var warning = false;

        if (stats.TotalBytes <= 0)
        {
            invalid = true;
            messages.Add("Không nhận byte nào trong phiên ghi.");
        }

        if (stats.RawFileSize == 0 && stats.TotalBytes > 0)
        {
            invalid = true;
            messages.Add("Raw file rỗng.");
        }

        if (stats.TotalBytes > 0 && stats.RawFileSize != stats.TotalBytes)
        {
            invalid = true;
            messages.Add($"Raw file size ({stats.RawFileSize}) không khớp total bytes ({stats.TotalBytes}).");
        }

        if (!stats.TextLogSaved || !stats.RawFileSaved || !stats.SessionJsonSaved)
        {
            invalid = true;
            messages.Add("File output chưa đầy đủ (.log, .raw.bin, .session.json).");
        }

        if (stats.PortErrorDuringCapture)
        {
            invalid = true;
            messages.Add("Cổng gặp lỗi trong lúc ghi.");
        }

        if (stats.IsStableSession && stats.Duration.TotalSeconds < MinStableDurationSeconds)
        {
            invalid = true;
            messages.Add($"Phiên stable chưa đạt tối thiểu {MinStableDurationSeconds:0} giây.");
        }

        if (stats.IsStableSession && !stats.HasKnownWeight)
        {
            warning = true;
            messages.Add("Known weight chưa được nhập cho phiên stable.");
        }

        if (stats.UniqueByteValues.Count is 1 or 2)
        {
            warning = true;
            var hex = string.Join(", ", stats.UniqueByteValues.Select(b => b.ToString("X2")));
            messages.Add($"Chỉ có {stats.UniqueByteValues.Count} giá trị byte duy nhất ({hex}) — cần kiểm tra thêm, không phải lỗi.");
        }

        if (stats.LongestNoDataGapMs > MaxNoDataGapWarningMs)
        {
            warning = true;
            messages.Add($"Có khoảng không dữ liệu dài {stats.LongestNoDataGapMs:F0} ms.");
        }

        if (stats.SessionType == GuidedSessionType.EmptyPersonTransition)
        {
            var missing = CaptureEventTypes.TransitionRequired
                .Where(t => stats.Events.All(e => e.EventType != t))
                .ToList();
            if (missing.Count > 0)
            {
                if (missing.Count >= 3)
                {
                    invalid = true;
                    messages.Add($"Thiếu quá nhiều marker transition: {string.Join(", ", missing)}.");
                }
                else
                {
                    warning = true;
                    messages.Add($"Thiếu marker transition: {string.Join(", ", missing)}.");
                }
            }
        }

        if (invalid)
            return new CaptureQualityResult { Status = CaptureQualityStatus.Invalid, Messages = messages };

        if (warning || messages.Count > 0)
            return new CaptureQualityResult { Status = CaptureQualityStatus.Warning, Messages = messages };

        messages.Add("Capture hợp lệ.");
        return new CaptureQualityResult { Status = CaptureQualityStatus.Valid, Messages = messages };
    }
}
