namespace CanXe.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
