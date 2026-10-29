namespace CanXe.Debt.Data;

public enum PaymentMethod
{
    Cash = 0,
    Transfer = 1,

    /// <summary>Settled against something else the seller owes (for example green beans sold to them), no money moves.</summary>
    Offset = 2
}

public enum UserRole
{
    Admin = 0,
    Accountant = 1
}

public sealed class Season
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsClosed { get; set; }

    public bool Contains(DateOnly date) => date >= StartDate && (EndDate is null || date <= EndDate);
}

public sealed class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Phone { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>An alternative spelling of a customer name that resolves to the same customer.</summary>
public sealed class CustomerAlias
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public required string RawName { get; set; }
    public required string NormalizedName { get; set; }
}

public sealed class TicketRecord
{
    public int Id { get; set; }
    public required string StationCode { get; set; }
    public required string Code { get; set; }
    public required string DisplayNumber { get; set; }
    public DateOnly TicketDate { get; set; }
    public DateTimeOffset TicketDateTime { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerNameRaw { get; set; }
    public string? CustomerNameNormalized { get; set; }

    public string? LicensePlate { get; set; }
    public string? Cargo { get; set; }
    public decimal? GrossWeightKg { get; set; }
    public decimal? TareWeightKg { get; set; }
    public decimal? NetWeightKg { get; set; }
    public decimal? BillableWeightKg { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? SplitFirstWeightKg { get; set; }
    public decimal? SecondUnitPrice { get; set; }
    public long? AmountVnd { get; set; }

    /// <summary>The truck has been weighed once and is waiting for the empty-truck weighing.</summary>
    public bool AwaitingSecondWeigh { get; set; }

    /// <summary>The only weight known while <see cref="AwaitingSecondWeigh"/>.</summary>
    public decimal? FirstWeightKg { get; set; }

    /// <summary>The note typed at the scale (for example a deposit tag or a lot name).</summary>
    public string? Notes { get; set; }

    /// <summary>The settlement period this truck was gathered into; null while it is still waiting to be settled.</summary>
    public int? PeriodId { get; set; }

    /// <summary>
    /// An admin's correction of the weight paid for (the seller's warehouse scale said 12.050, ours 12.000, and 12.050 is what is paid).
    /// The Original* values keep what the weighing app sent; a re-import refreshes them and the correction is applied on top again.
    /// </summary>
    public decimal? AdjustedNetKg { get; set; }
    public decimal? AdjustedUnitPrice { get; set; }
    public decimal? OriginalNetKg { get; set; }
    public decimal? OriginalBillableKg { get; set; }
    public decimal? OriginalUnitPrice { get; set; }
    public long? OriginalAmountVnd { get; set; }
    public DateTimeOffset? AdjustedAt { get; set; }
    public string? AdjustedBy { get; set; }
    public string? AdjustReason { get; set; }

    /// <summary>One line per correction ("when · who: field: old → new · reason"), oldest first.</summary>
    public string? AdjustLog { get; set; }

    public bool IsAdjusted => AdjustedNetKg is not null || AdjustedUnitPrice is not null;

    /// <summary>
    /// Set on the held-back part of a truck that was cut in two ("tách xe"): the ticket it was cut from. The part never comes
    /// from a weighing file, so a re-import leaves it alone; it waits in the open pool with no price until it is sold.
    /// </summary>
    public int? SplitFromTicketId { get; set; }

    public bool IsDeleted { get; set; }
    public required string ContentHash { get; set; }
    public int ImportBatchId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool HasPriceSplit => SplitFirstWeightKg is > 0 && SecondUnitPrice is not null;

    /// <summary>Where the ticket is on its way to becoming money owed.</summary>
    public TicketStage Stage => AwaitingSecondWeigh ? TicketStage.AwaitingSecondWeigh : AmountVnd is null ? TicketStage.NoPrice : TicketStage.Priced;
}

public enum TicketStage
{
    /// <summary>Only the first weighing exists, so there is no weight to pay for yet.</summary>
    AwaitingSecondWeigh,

    /// <summary>Weighed but no unit price yet, so no amount yet.</summary>
    NoPrice,
    Priced
}

/// <summary>
/// A settlement period ("lô") of one seller: a set of trucks chosen to be paid together, which can be a run of tickets, a
/// few trucks of the same day or a single truck. <see cref="FromDate"/> and <see cref="ToDate"/> are the dates of its
/// first and last truck. Whatever is left unpaid carries into the next period.
/// </summary>
public sealed class SettlementPeriod
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int SeasonId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public DateTimeOffset ClosedAt { get; set; }
    public required string ClosedBy { get; set; }
}

public sealed class Payment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int SeasonId { get; set; }

    /// <summary>The settlement period this payment is for; null means the still-open period (paid ahead).</summary>
    public int? PeriodId { get; set; }
    public DateOnly PaidOn { get; set; }
    public long AmountVnd { get; set; }
    public PaymentMethod Method { get; set; }

    /// <summary>Bank the money went to, for transfers.</summary>
    public string? BankName { get; set; }

    /// <summary>Who handed the money over on our side ("Bố giao").</summary>
    public string? PaidBy { get; set; }

    /// <summary>Who collected the money ("A Thắng nhận giùm").</summary>
    public string? ReceivedBy { get; set; }
    public string? Note { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? EditedAt { get; set; }
    public string? EditedBy { get; set; }

    /// <summary>One line per correction ("when · who: field: old → new"), oldest first, so a fixed payment still shows what it used to say.</summary>
    public string? EditLog { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public bool IsVoided => VoidedAt is not null;
}

/// <summary>
/// An extra amount owed to the seller on top of the trucks of a lot (a truck fare, a payment to make up for short goods), or a
/// negative one to deduct. It raises or lowers what the lot has to pay; like a payment it is never deleted, only voided.
/// </summary>
public sealed class PeriodExtra
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int SeasonId { get; set; }
    public int PeriodId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>What it is for ("Tiền xe", "Tiền bù hàng", …).</summary>
    public required string Label { get; set; }

    /// <summary>Positive adds to what is owed to the seller, negative deducts.</summary>
    public long AmountVnd { get; set; }
    public string? Note { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public bool IsVoided => VoidedAt is not null;
}

public sealed class ImportBatch
{
    public int Id { get; set; }
    public required string StationCode { get; set; }
    public required string StationName { get; set; }
    public required string FileName { get; set; }
    public DateTimeOffset ExportedAt { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public required string ImportedBy { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int TicketCount { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Skipped { get; set; }
}

public sealed class AppUser
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Secret of the authenticator app (base32). Set while setting up, in use once <see cref="TotpEnabled"/>.</summary>
    public string? TotpSecret { get; set; }
    public bool TotpEnabled { get; set; }

    /// <summary>The last 30-second step a code was accepted for, so the same code cannot be used twice.</summary>
    public long TotpLastStep { get; set; }

    /// <summary>One-time recovery codes for a lost phone, stored only as SHA-256 hashes, separated by commas.</summary>
    public string? RecoveryCodeHashes { get; set; }
}

public sealed class AppSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
