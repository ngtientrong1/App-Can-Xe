namespace CanXe.Domain.Entities;

public class WeighTicket
{
    public int Id { get; set; }
    public int SequenceNumber { get; set; }
    public int TicketYear { get; set; }
    public int TicketMonth { get; set; }
    public required string InternalCode { get; set; }
    public required string DisplayNumber { get; set; }
    public DateTimeOffset TicketDateTime { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerNameSnapshot { get; set; }

    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public string? LicensePlateSnapshot { get; set; }

    public int? CargoTypeId { get; set; }
    public CargoType? CargoType { get; set; }
    public string? CargoTypeNameSnapshot { get; set; }

    public int? UnitPriceVndPerKg { get; set; }
    public string? Notes { get; set; }

    public int? GrossWeightGrams { get; set; }
    public int? TareWeightGrams { get; set; }
    public int? NetWeightGrams { get; set; }
    public int? DeductionWeightGrams { get; set; }
    public int? BillableWeightGrams { get; set; }
    public int? TotalAmountVnd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public string? DeleteReason { get; set; }

    public ICollection<WeighEvent> Events { get; set; } = [];
}
