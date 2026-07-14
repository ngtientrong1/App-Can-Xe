namespace CanXe.Domain.Entities;

public class Vehicle
{
    public int Id { get; set; }
    public required string PlateNumber { get; set; }
    public required string NormalizedPlateNumber { get; set; }
    public string? OwnerName { get; set; }
    public string? Note { get; set; }
    public int? LastCustomerId { get; set; }
    public Customer? LastCustomer { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
