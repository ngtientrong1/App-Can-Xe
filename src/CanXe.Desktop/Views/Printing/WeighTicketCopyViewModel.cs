using CanXe.Application.Models;
using CanXe.Application.Services;

namespace CanXe.Desktop.Views.Printing;

public sealed class WeighTicketCopyViewModel
{
    public required string StationName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public required string StationSubtitle { get; init; }
    public required string DisplayNumber { get; init; }
    public required string CustomerName { get; init; }
    public required string LicensePlate { get; init; }
    public required string CargoTypeName { get; init; }
    public required string GrossWeightDisplay { get; init; }
    public required string TareWeightDisplay { get; init; }
    public required string NetWeightDisplay { get; init; }
    public required string GrossWeightValue { get; init; }
    public string? GrossWeightUnit { get; init; }
    public bool ShowGrossWeightUnit { get; init; }
    public required string TareWeightValue { get; init; }
    public string? TareWeightUnit { get; init; }
    public bool ShowTareWeightUnit { get; init; }
    public required string NetWeightValue { get; init; }
    public string? NetWeightUnit { get; init; }
    public bool ShowNetWeightUnit { get; init; }
    public required string UnitPrice { get; init; }
    public required string TotalAmount { get; init; }
    public required string DeductionWeightDisplay { get; init; }
    public required string Notes { get; init; }
    public required string Weigh1Time { get; init; }
    public required string Weigh1Date { get; init; }
    public required string Weigh2Time { get; init; }
    public required string Weigh2Date { get; init; }
    public required string SignDateText { get; init; }
    public bool Weigh1Recorded { get; init; }
    public bool Weigh2Recorded { get; init; }
    public bool ShowReprintWatermark { get; init; }

    public static WeighTicketCopyViewModel From(WeighTicketPrintModel model)
    {
        var unitPrice = WeighTicketPrintFormatter.FormatUnitPricePerKg(model.UnitPriceVndPerKg, model.ShowPrice);
        var totalAmount = WeighTicketPrintFormatter.FormatTotalAmount(model.TotalAmountVnd, model.ShowPrice);
        var weigh1Recorded = WeighTicketPrintFormatter.HasRecordedWeighTimestamp(model.FirstWeighingAt);
        var weigh2Recorded = WeighTicketPrintFormatter.HasRecordedWeighTimestamp(model.SecondWeighingAt);
        var bothWeighingsRecorded = weigh1Recorded && weigh2Recorded;

        var grossDisplay = WeighTicketPrintFormatter.FormatHeroWeightDisplay(model.GrossWeightKg, model.FirstWeighingAt);
        var tareDisplay = WeighTicketPrintFormatter.FormatHeroWeightDisplay(model.TareWeightKg, model.SecondWeighingAt);
        var netDisplay = WeighTicketPrintFormatter.FormatHeroWeightDisplay(model.NetWeightKg, bothWeighingsRecorded);
        var (grossValue, grossUnit, showGrossUnit) = SplitHeroWeightDisplay(grossDisplay);
        var (tareValue, tareUnit, showTareUnit) = SplitHeroWeightDisplay(tareDisplay);
        var (netValue, netUnit, showNetUnit) = SplitHeroWeightDisplay(netDisplay);

        return new WeighTicketCopyViewModel
        {
            StationName = model.Station.StationName.ToUpperInvariant(),
            Address = model.Station.Address,
            Phone = string.IsNullOrWhiteSpace(model.Station.Phone) ? null : $"Điện thoại: {model.Station.Phone}",
            StationSubtitle = (model.Station.StationSubtitle ?? "TRẠM CÂN ĐIỆN TỬ 60 TẤN").ToUpperInvariant(),
            DisplayNumber = model.DisplayNumber,
            CustomerName = NullToDash(model.CustomerName),
            LicensePlate = NullToDash(model.LicensePlate),
            CargoTypeName = NullToDash(model.CargoTypeName),
            GrossWeightDisplay = grossDisplay,
            TareWeightDisplay = tareDisplay,
            NetWeightDisplay = netDisplay,
            GrossWeightValue = grossValue,
            GrossWeightUnit = grossUnit,
            ShowGrossWeightUnit = showGrossUnit,
            TareWeightValue = tareValue,
            TareWeightUnit = tareUnit,
            ShowTareWeightUnit = showTareUnit,
            NetWeightValue = netValue,
            NetWeightUnit = netUnit,
            ShowNetWeightUnit = showNetUnit,
            UnitPrice = unitPrice ?? "—",
            TotalAmount = totalAmount ?? "—",
            DeductionWeightDisplay = WeighTicketPrintFormatter.FormatWeightKg(model.DeductionWeightKg),
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? "—" : model.Notes,
            Weigh1Recorded = weigh1Recorded,
            Weigh2Recorded = weigh2Recorded,
            Weigh1Time = WeighTicketPrintFormatter.FormatTime(model.FirstWeighingAt),
            Weigh1Date = WeighTicketPrintFormatter.FormatDate(model.FirstWeighingAt),
            Weigh2Time = WeighTicketPrintFormatter.FormatTime(model.SecondWeighingAt),
            Weigh2Date = WeighTicketPrintFormatter.FormatDate(model.SecondWeighingAt),
            SignDateText = WeighTicketPrintFormatter.FormatSignDate(model.TicketDateTime, model.Station.SignLocationName),
            ShowReprintWatermark = model.IsReprint && model.PrintSettings.ShowReprintWatermark
        };
    }

    private static string NullToDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static (string Value, string? Unit, bool ShowUnit) SplitHeroWeightDisplay(string display)
    {
        if (display == "—")
            return ("—", null, false);

        const string unit = " kg";
        if (display.EndsWith(unit, StringComparison.Ordinal))
            return (display[..^unit.Length], "kg", true);

        return (display, null, false);
    }
}
