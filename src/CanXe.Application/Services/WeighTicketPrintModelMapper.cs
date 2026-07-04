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
            ? $"{at.Value.ToString("HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture)} • {at.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.CurrentCulture)}"
            : "—";

    public static string FormatTime(DateTimeOffset? at) =>
        at?.ToString("HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture) ?? "—";

    public static string FormatDate(DateTimeOffset? at) =>
        at?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.CurrentCulture) ?? "—";

    public static string? FormatUnitPricePerKg(decimal? vndPerKg, bool showPrice)
    {
        if (!showPrice || !vndPerKg.HasValue || vndPerKg.Value <= 0)
            return null;
        return $"{vndPerKg.Value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)} VNĐ/kg";
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
            UnitPriceVndPerKg = detail.UnitPriceVndPerKg,
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
