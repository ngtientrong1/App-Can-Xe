namespace CanXe.Domain.Entities;

public sealed class StationSettings
{
    public int Id { get; set; } = 1;
    public string StationName { get; set; } = "Trạm cân CanXe";
    public string? StationSubtitle { get; set; }
    public string? OwnerName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxCode { get; set; }
    public string? LogoPath { get; set; }
    public string? TicketFooterText { get; set; }
    public string? SignLocationName { get; set; }

    /// <summary>
    /// Code of this scale station in the debt app (for example DAKHA). A ticket is identified there by this code plus
    /// its ticket number, so each station needs its own.
    /// </summary>
    public string? DebtStationCode { get; set; }
    public string? DebtStationName { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
