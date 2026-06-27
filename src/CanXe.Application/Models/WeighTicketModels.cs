namespace CanXe.Application.Models;

public sealed class WeighEventDraft
{
    public int Sequence { get; init; }
    public decimal WeightKg { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public int? PersistedEventId { get; set; }
    public string? PhotoPath { get; set; }
    public bool PhotoCaptureSucceeded { get; set; }
    public string? PhotoErrorMessage { get; set; }
}

public sealed class WeighTicketDraft
{
    public int? ExistingTicketId { get; set; }
    public string? DisplayNumber { get; set; }
    public string? InternalCode { get; set; }
    public DateTimeOffset? TicketDateTime { get; set; }
    public string? CustomerName { get; set; }
    public string? LicensePlate { get; set; }
    public string? CargoTypeName { get; set; }
    public decimal? UnitPriceVndPerKg { get; set; }
    public string? Notes { get; set; }
    public List<WeighEventDraft> Events { get; } = [];
}

public sealed class WeighTicketFilter
{
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public string? CustomerName { get; set; }
    public string? CargoTypeName { get; set; }
    public string? LicensePlate { get; set; }
    public string? DisplayNumber { get; set; }
    public decimal? UnitPriceVndPerKg { get; set; }
}

public sealed class WeighTicketListItem
{
    public int Id { get; init; }
    public DateTimeOffset TicketDateTime { get; init; }
    public required string DisplayNumber { get; init; }
    public string? LicensePlate { get; init; }
    public string? CustomerName { get; init; }
    public string? CargoTypeName { get; init; }
    public decimal? Weight1Kg { get; init; }
    public decimal? Weight2Kg { get; init; }
    public decimal? GrossWeightKg { get; init; }
    public decimal? TareWeightKg { get; init; }
    public decimal? NetWeightKg { get; init; }
    public decimal? BillableWeightKg { get; init; }
    public decimal? UnitPriceVndPerKg { get; init; }
    public decimal? TotalAmountVnd { get; init; }
    public string? Notes { get; init; }
    public int EventCount { get; init; }
}

public sealed class SaveTicketResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public WeighTicketListItem? SavedTicket { get; init; }
    public IReadOnlyList<string> SimilarCustomerWarnings { get; init; } = [];
}

public sealed class RecordWeightResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public WeighEventDraft? Event { get; init; }
    public bool IsRecordingLocked { get; init; }
}

public sealed class SimilarCustomerWarning
{
    public required string EnteredName { get; init; }
    public required string SimilarName { get; init; }
}
