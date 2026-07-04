namespace CanXe.Application.Models;

public sealed class PrintSettingsDto
{
    public string? PreferredPrinterName { get; set; }
    public string PaperSize { get; set; } = "A4";
    public string PaperOrientation { get; set; } = "Portrait";
    public int DefaultCopies { get; set; } = 1;
    public string TopCopyLabel { get; set; } = "LIÊN TRẠM CÂN";
    public string BottomCopyLabel { get; set; } = "LIÊN KHÁCH HÀNG";
    public bool ShowLogo { get; set; } = true;
    public bool ShowPrice { get; set; } = true;
    public string? TicketFooterText { get; set; }
    public string? SignLocationName { get; set; }
    public bool ShowReprintWatermark { get; set; } = true;
    public PrintRenderingMode PrintRenderingMode { get; set; } = PrintRenderingMode.RasterCompatibility;
    public PrintLayoutMode PrintLayoutMode { get; set; } = PrintLayoutMode.A4TwoUp;
}

public sealed class PrintJobHistoryDto
{
    public long Id { get; init; }
    public int? TicketId { get; init; }
    public string? TicketNumber { get; init; }
    public string? PrinterName { get; init; }
    public string PaperSize { get; init; } = "A4";
    public int Copies { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string Status { get; init; } = "Prepared";
    public string? ErrorMessage { get; init; }
    public bool IsReprint { get; init; }
}

public sealed class WeighTicketPrintModel
{
    public required int TicketId { get; init; }
    public required string DisplayNumber { get; init; }
    public required DateTimeOffset TicketDateTime { get; init; }
    public string? CustomerName { get; init; }
    public string? LicensePlate { get; init; }
    public string? CargoTypeName { get; init; }
    public string? Notes { get; init; }
    public decimal? GrossWeightKg { get; init; }
    public decimal? TareWeightKg { get; init; }
    public decimal? NetWeightKg { get; init; }
    public decimal? Weight1Kg { get; init; }
    public decimal? Weight2Kg { get; init; }
    public decimal? UnitPriceVndPerKg { get; init; }
    public decimal? TotalAmountVnd { get; init; }
    public bool ShowPrice { get; init; } = true;
    public DateTimeOffset? Weight1RecordedAt { get; init; }
    public DateTimeOffset? Weight2RecordedAt { get; init; }

    /// <summary>Timestamp độc lập lần cân 1 — từ WeighEvent.RecordedAt, không phải CreatedAt.</summary>
    public DateTimeOffset? FirstWeighingAt => Weight1RecordedAt;

    /// <summary>Timestamp độc lập lần cân 2 — từ WeighEvent.RecordedAt, không phải UpdatedAt.</summary>
    public DateTimeOffset? SecondWeighingAt => Weight2RecordedAt;

    public required StationHeaderModel Station { get; init; }
    public required PrintSettingsDto PrintSettings { get; init; }
    public bool IsReprint { get; init; }
}

public sealed class StationHeaderModel
{
    public required string StationName { get; init; }
    public string? StationSubtitle { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? LogoPath { get; init; }
    public bool ShowLogo { get; init; }
    public string? FooterText { get; init; }
    public string? SignLocationName { get; init; }
}
