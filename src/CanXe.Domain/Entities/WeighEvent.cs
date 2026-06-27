namespace CanXe.Domain.Entities;

public class WeighEvent
{
    public int Id { get; set; }
    public int WeighTicketId { get; set; }
    public WeighTicket? WeighTicket { get; set; }
    public int Sequence { get; set; }
    public int WeightGrams { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string? PhotoPath { get; set; }
    public bool PhotoCaptureSucceeded { get; set; }
    public string? PhotoErrorMessage { get; set; }
    public string? RawScaleData { get; set; }
}
