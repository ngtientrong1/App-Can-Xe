using CanXe.Domain.Entities;

namespace CanXe.Domain.Services;

public static class WeighEventWeightResolver
{
    public static int GetEffectiveWeightGrams(WeighEvent weighEvent) =>
        weighEvent.OverrideWeightGrams ?? weighEvent.OriginalWeightGrams;

    public static bool HasManualOverride(WeighEvent weighEvent) =>
        weighEvent.IsManualOverride && weighEvent.OverrideWeightGrams.HasValue;
}

public static class WeightOverrideReasons
{
    public const string WrongEntry = "wrong_entry";
    public const string DeviceError = "device_error";
    public const string MissingWeigh = "missing_weigh";
    public const string DocumentAdjust = "document_adjust";
    public const string WrongWeigh = "wrong_weigh";
    public const string Other = "other";

    public static readonly IReadOnlyList<(string Code, string Label)> All =
    [
        (WrongEntry, "Nhập sai số cân"),
        (DeviceError, "Thiết bị cân lỗi"),
        (MissingWeigh, "Bổ sung số cân còn thiếu"),
        (DocumentAdjust, "Điều chỉnh theo chứng từ"),
        (WrongWeigh, "Cân nhầm"),
        (Other, "Lý do khác")
    ];

    public static string? ResolveDisplayReason(string? code, string? otherText)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        if (code == Other)
            return string.IsNullOrWhiteSpace(otherText) ? null : otherText.Trim();

        return All.FirstOrDefault(x => x.Code == code).Label;
    }

    public static bool RequiresOtherText(string? code) => code == Other;
}

public static class TicketListSorter
{
    public static IReadOnlyList<T> SortNewestFirst<T>(
        IEnumerable<T> items,
        Func<T, DateTimeOffset> getTicketDateTime,
        Func<T, int> getSequenceNumber,
        Func<T, int> getId)
    {
        return items
            .OrderByDescending(getTicketDateTime)
            .ThenByDescending(getSequenceNumber)
            .ThenByDescending(getId)
            .ToList();
    }
}

public static class TicketEditValidator
{
    public static string? ValidateWeightOverrideReason(string? reasonCode, string? reasonOther)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            return "Phải chọn lý do sửa trọng lượng.";

        if (WeightOverrideReasons.RequiresOtherText(reasonCode) &&
            string.IsNullOrWhiteSpace(reasonOther))
            return "Phải nhập lý do khác.";

        return null;
    }
}
