namespace CanXe.Domain.Entities;

public class CargoType
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public int? DefaultUnitPriceVndPerKg { get; set; }
    public string Unit { get; set; } = "kg";
    public string? Note { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
