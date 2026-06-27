namespace CanXe.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public int TicketId { get; set; }
    public required string FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset EditedAt { get; set; }
    public string? EditedBy { get; set; }
    public bool IsDeveloperOverride { get; set; }
}
