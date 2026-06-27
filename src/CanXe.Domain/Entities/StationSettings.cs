namespace CanXe.Domain.Entities;

public sealed class StationSettings
{
    public int Id { get; set; } = 1;
    public string StationName { get; set; } = "Trạm cân CanXe";
    public string? OwnerName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxCode { get; set; }
    public string? LogoPath { get; set; }
    public string? TicketFooterText { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
