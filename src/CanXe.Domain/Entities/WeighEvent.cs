using CanXe.Domain.Models;

namespace CanXe.Domain.Entities;

public class WeighEvent
{
    public int Id { get; set; }
    public int WeighTicketId { get; set; }
    public WeighTicket? WeighTicket { get; set; }
    public int Sequence { get; set; }
    public int OriginalWeightGrams { get; set; }
    public int? OverrideWeightGrams { get; set; }
    public bool IsManualOverride { get; set; }
    public string? OverrideReason { get; set; }
    public DateTimeOffset? OverrideAt { get; set; }
    public string? OverrideBy { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string? PhotoPath { get; set; }
    public bool PhotoCaptureSucceeded { get; set; }
    public string? PhotoErrorMessage { get; set; }
    public string? RawScaleData { get; set; }

    /// <summary>Hardware (default for legacy rows) or Manual admin entry.</summary>
    public WeighInputSource InputSource { get; set; } = WeighInputSource.Hardware;

    public string? ManualReason { get; set; }

    public StationUserRole CreatedByRole { get; set; } = StationUserRole.Operator;

    public int EffectiveWeightGrams => OverrideWeightGrams ?? OriginalWeightGrams;
}
