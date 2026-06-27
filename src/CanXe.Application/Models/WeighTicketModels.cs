namespace CanXe.Application.Models;

public sealed class WeighTicketDraft
{
    public Guid DraftSessionId { get; } = Guid.NewGuid();

    public int? ExistingTicketId { get; set; }
    public string? DisplayNumber { get; set; }
    public string? InternalCode { get; set; }
    public DateTimeOffset? TicketDateTime { get; set; }

    public string? DraftCustomer { get; set; }
    public string? DraftVehicle { get; set; }
    public string? DraftCargoType { get; set; }
    public decimal? DraftUnitPrice { get; set; }
    public string? DraftNotes { get; set; }

    public decimal? DraftWeight1 { get; set; }
    public DateTimeOffset? DraftWeight1RecordedAt { get; set; }
    public string? DraftWeight1PhotoPath { get; set; }
    public DraftPhotoStatus DraftWeight1PhotoStatus { get; set; }
    public string? DraftWeight1PhotoError { get; set; }
    public bool IsWeight1LockedFromSavedTicket { get; set; }
    public int? SavedWeight1EventId { get; set; }

    public decimal? DraftWeight2 { get; set; }
    public DateTimeOffset? DraftWeight2RecordedAt { get; set; }
    public string? DraftWeight2PhotoPath { get; set; }
    public DraftPhotoStatus DraftWeight2PhotoStatus { get; set; }
    public string? DraftWeight2PhotoError { get; set; }
    public bool IsWeight2LockedFromSavedTicket { get; set; }
    public int? SavedWeight2EventId { get; set; }

    public bool HasAnyWeight => DraftWeight1.HasValue || DraftWeight2.HasValue;

    public decimal? GetWeightKg(int sequence) => sequence == 1 ? DraftWeight1 : DraftWeight2;

    public void SetWeightDraft(int sequence, decimal weightKg, DateTimeOffset recordedAt)
    {
        if (sequence == 1)
        {
            DraftWeight1 = weightKg;
            DraftWeight1RecordedAt = recordedAt;
        }
        else
        {
            DraftWeight2 = weightKg;
            DraftWeight2RecordedAt = recordedAt;
        }
    }

    public void InvalidatePhotoDraft(int sequence)
    {
        if (sequence == 1)
        {
            DraftWeight1PhotoPath = null;
            DraftWeight1PhotoStatus = DraftPhotoStatus.Failed;
        }
        else
        {
            DraftWeight2PhotoPath = null;
            DraftWeight2PhotoStatus = DraftPhotoStatus.Failed;
        }
    }

    public void SetPhotoPending(int sequence)
    {
        if (sequence == 1)
            DraftWeight1PhotoStatus = DraftPhotoStatus.Pending;
        else
            DraftWeight2PhotoStatus = DraftPhotoStatus.Pending;
    }
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
    public int MaxResults { get; set; } = 200;
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

public sealed class CaptureWeightResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int Sequence { get; init; }
    public decimal WeightKg { get; init; }
    public bool IsUpdate { get; init; }
}

public sealed class VehicleSuggestion
{
    public required string PlateNumber { get; init; }
    public string? LastCustomerName { get; init; }
}
