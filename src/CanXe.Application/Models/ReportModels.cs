namespace CanXe.Application.Models;

public enum ReportWorkflowFilter
{
    All,
    Completed,
    AwaitingSecondWeigh,
    MissingPrice
}

public sealed class ReportFilter
{
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public string? CustomerKeyword { get; set; }
    public string? LicensePlateKeyword { get; set; }
    public string? CargoTypeKeyword { get; set; }
    public ReportWorkflowFilter State { get; set; } = ReportWorkflowFilter.All;
    public bool IncludeDeleted { get; set; }
    public int MaxResults { get; set; } = 5000;
}

public sealed class ReportSummaryDto
{
    public int TicketCount { get; init; }
    public decimal TotalGrossWeightKg { get; init; }
    public decimal TotalTareWeightKg { get; init; }
    public decimal TotalNetWeightKg { get; init; }
    public decimal TotalBillableWeightKg { get; init; }
    public decimal TotalAmountVnd { get; init; }
    public int MissingPriceCount { get; init; }
    public int AwaitingSecondWeighCount { get; init; }
}

public sealed class ReportRowDto
{
    public int TicketId { get; init; }
    public DateTimeOffset TicketDateTime { get; init; }
    public string DisplayNumber { get; init; } = string.Empty;
    public string WorkflowStatusText { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public string? LicensePlate { get; init; }
    public string? CargoTypeName { get; init; }
    public decimal? GrossWeightKg { get; init; }
    public decimal? TareWeightKg { get; init; }
    public decimal? NetWeightKg { get; init; }
    public decimal? BillableWeightKg { get; init; }
    public decimal? UnitPriceVndPerKg { get; init; }
    public decimal? TotalAmountVnd { get; init; }
    public string? Notes { get; init; }
}

public sealed class ReportResult
{
    public ReportSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<ReportRowDto> Rows { get; init; } = Array.Empty<ReportRowDto>();
}

public sealed class ExportResult
{
    public bool Success { get; init; }
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }

    public static ExportResult Succeeded(string filePath) =>
        new() { Success = true, FilePath = filePath };

    public static ExportResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };

    public static ExportResult Cancelled() =>
        new() { Success = false, ErrorMessage = "cancelled" };
}
