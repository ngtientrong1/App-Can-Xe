namespace CanXe.Domain.Entities;

public enum PrintJobStatus
{
    Prepared,
    Submitted,
    Failed,
    Cancelled
}

public sealed class PrintSettings
{
    public int Id { get; set; } = 1;
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
    public string PrintRenderingMode { get; set; } = "RasterCompatibility";
    public string PrintLayoutMode { get; set; } = "A4TwoUp";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PrintJobHistory
{
    public long Id { get; set; }
    public int? TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public string? PrinterName { get; set; }
    public string PaperSize { get; set; } = "A4";
    public int Copies { get; set; } = 1;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Prepared;
    public string? ErrorMessage { get; set; }
    public bool IsReprint { get; set; }
}
