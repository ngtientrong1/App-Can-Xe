using System.Globalization;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public static class PriceSplitValidator
{
    /// <summary>Error text for an incomplete or impossible two-level price on the draft, or null when fine.</summary>
    public static string? Validate(WeighTicketDraft draft)
    {
        var first = draft.DraftSplitFirstWeightKg;
        var second = draft.DraftSecondUnitPrice;
        if (first is null && second is null)
            return null;

        if (first is not > 0 || second is not > 0)
            return "Tách giá: nhập đủ khối lượng phần đầu và giá phần còn lại (đều lớn hơn 0).";

        if (draft.DraftUnitPrice is not > 0)
            return "Tách giá: cần nhập đơn giá cho phần đầu.";

        var net = WeightCalculator.Calculate(draft.DraftWeight1, draft.DraftWeight2, draft.DraftUnitPrice).NetWeightKg;
        if (net is { } kg && first.Value >= kg)
        {
            return string.Create(CultureInfo.CurrentCulture,
                $"Tách giá: khối lượng phần đầu ({first.Value:N0} kg) phải nhỏ hơn khối lượng hàng ({kg:N0} kg).");
        }

        return null;
    }
}
