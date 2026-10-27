using System.Globalization;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public static class WeighTicketPrintFormatter
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string FormatWeightKg(decimal? value)
    {
        if (!value.HasValue)
            return "—";

        var rounded = Math.Round(value.Value, 0, MidpointRounding.AwayFromZero);
        return $"{rounded.ToString("N0", VietnameseCulture)} kg";
    }

    public static string FormatWeightKgCompact(decimal? kg) =>
        !kg.HasValue
            ? "—"
            : Math.Round(kg.Value, 0, MidpointRounding.AwayFromZero).ToString("N0", VietnameseCulture);

    public static string FormatHeroWeightValue(decimal? kg, DateTimeOffset? recordedAt) =>
        FormatHeroWeightValue(kg, HasRecordedWeighTimestamp(recordedAt));

    public static string FormatHeroWeightValue(decimal? kg, bool eventRecorded)
    {
        if (!eventRecorded)
            return "—";

        if (!kg.HasValue)
            return "—";

        return Math.Round(kg.Value, 0, MidpointRounding.AwayFromZero).ToString("N0", VietnameseCulture);
    }

    public static string FormatHeroWeightDisplay(decimal? kg, DateTimeOffset? recordedAt) =>
        FormatHeroWeightDisplay(kg, HasRecordedWeighTimestamp(recordedAt));

    public static string FormatHeroWeightDisplay(decimal? kg, bool eventRecorded)
    {
        if (!eventRecorded)
            return "—";

        if (!kg.HasValue)
            return "—";

        return FormatWeightKg(kg);
    }

    public static bool HasRecordedWeighTimestamp(DateTimeOffset? recordedAt) =>
        recordedAt.HasValue;

    public static bool HasRecordedWeighEvent(decimal? kg, DateTimeOffset? recordedAt) =>
        recordedAt.HasValue && HasMeaningfulWeight(kg);

    public static bool HasMeaningfulWeight(decimal? kg) =>
        kg is > 0;

    public static string FormatWeighCardTimestamp(DateTimeOffset? at) =>
        at.HasValue
            ? $"{FormatTime(at)} • {FormatDate(at)}"
            : "—";

    // Fixed separators: with the culture's own, a Windows short-date such as dd-MM-yy prints "29-06-2026" on the ticket.
    public static string FormatTime(DateTimeOffset? at) =>
        at?.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    public static string FormatDate(DateTimeOffset? at) =>
        at?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    public static string? FormatUnitPricePerKg(decimal? vndPerKg, bool showPrice)
    {
        if (!showPrice || !vndPerKg.HasValue || vndPerKg.Value <= 0)
            return null;
        return $"{vndPerKg.Value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)} VNĐ/kg";
    }

    /// <summary>"19.800 / 19.600 VNĐ/kg" for a two-level ticket, otherwise the single price.</summary>
    public static string? FormatUnitPrice(WeighTicketPrintModel model)
    {
        if (!model.ShowPrice || !(model.UnitPriceVndPerKg > 0))
            return null;

        // The printed cell is one line wide: the ĐƠN GIÁ label already says per kg, so the unit is dropped.
        return model.HasPriceSplit
            ? PriceTextFormatter.UnitPrice(model.UnitPriceVndPerKg, model.SecondUnitPriceVndPerKg, true)
            : FormatUnitPricePerKg(model.UnitPriceVndPerKg, model.ShowPrice);
    }

    /// <summary>"15.000 kg × 19.800 + 1.022 kg × 19.600" when the ticket is priced in two parts, else null.</summary>
    public static string? FormatPriceBreakdown(WeighTicketPrintModel model)
    {
        if (!model.ShowPrice || !model.HasPriceSplit || model.BillableWeightKg is not > 0 || model.UnitPriceVndPerKg is not > 0)
            return null;

        var lines = PriceBreakdown.Compute(
            model.BillableWeightKg.Value,
            model.UnitPriceVndPerKg.Value,
            PriceSplit.TryCreate(model.SplitFirstWeightKg, model.SecondUnitPriceVndPerKg));

        return lines.Count > 1 ? PriceTextFormatter.Breakdown(lines) : null;
    }

    public static string? FormatTotalAmount(decimal? amountVnd, bool showPrice)
    {
        if (!showPrice || !amountVnd.HasValue || amountVnd.Value <= 0)
            return null;
        return $"{amountVnd.Value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)} VNĐ";
    }

    public static string FormatSignDate(DateTimeOffset date, string? location)
    {
        var place = string.IsNullOrWhiteSpace(location) ? string.Empty : $"{location}, ";
        return $"{place}ngày {date.Day} tháng {date.Month:00} năm {date.Year}";
    }
}

public static class WeighTicketPrintModelMapper
{
    public static WeighTicketPrintModel FromDetail(
        WeighTicketDetailDto detail,
        StationSettingsDto station,
        PrintSettingsDto printSettings,
        bool isReprint = false) =>
        new()
        {
            TicketId = detail.Id,
            DisplayNumber = detail.DisplayNumber,
            TicketDateTime = detail.TicketDateTime,
            CustomerName = detail.CustomerName,
            LicensePlate = detail.LicensePlate,
            CargoTypeName = detail.CargoTypeName,
            Notes = detail.Notes,
            GrossWeightKg = detail.GrossWeightKg,
            TareWeightKg = detail.TareWeightKg,
            NetWeightKg = detail.NetWeightKg,
            DeductionWeightKg = detail.DeductionWeightKg,
            UnitPriceVndPerKg = detail.UnitPriceVndPerKg,
            BillableWeightKg = detail.BillableWeightKg,
            SplitFirstWeightKg = detail.SplitFirstWeightKg,
            SecondUnitPriceVndPerKg = detail.SecondUnitPriceVndPerKg,
            TotalAmountVnd = detail.TotalAmountVnd,
            ShowPrice = printSettings.ShowPrice && !detail.IsServiceWeigh,
            Weight1Kg = detail.Weight1Kg,
            Weight2Kg = detail.Weight2Kg,
            Weight1RecordedAt = detail.Weight1RecordedAt,
            Weight2RecordedAt = detail.Weight2RecordedAt,
            IsReprint = isReprint,
            PrintSettings = printSettings,
            Station = new StationHeaderModel
            {
                StationName = station.StationName,
                StationSubtitle = station.StationSubtitle,
                Address = station.Address,
                Phone = station.Phone,
                LogoPath = station.LogoPath,
                ShowLogo = printSettings.ShowLogo,
                FooterText = printSettings.TicketFooterText ?? station.TicketFooterText,
                SignLocationName = printSettings.SignLocationName ?? station.SignLocationName
            }
        };
}
