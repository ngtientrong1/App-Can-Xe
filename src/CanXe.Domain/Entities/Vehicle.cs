namespace CanXe.Domain.Entities;

public class Vehicle
{
    public int Id { get; set; }
    public required string PlateNumber { get; set; }
    public required string NormalizedPlateNumber { get; set; }
    public int? LastCustomerId { get; set; }
    public Customer? LastCustomer { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
