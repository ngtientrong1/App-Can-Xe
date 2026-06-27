namespace CanXe.Application.Models;

public sealed class AutocompleteSuggestionItem
{
    public required string PrimaryText { get; init; }
    public string? SecondaryText { get; init; }
    public string? TertiaryText { get; init; }
    public bool IsNewEntryOption { get; init; }
    public object? Tag { get; init; }

    public override string ToString() => PrimaryText;
}

public sealed class WeighTicketFilterResult
{
    public IReadOnlyList<WeighTicketListItem> Items { get; init; } = [];
    public int Count { get; init; }
    public decimal TotalNetWeightKg { get; init; }
    public decimal TotalBillableWeightKg { get; init; }
    public decimal TotalAmountVnd { get; init; }
    public int MissingPriceCount { get; init; }
}

public sealed class ActiveFilterChip
{
    public required string Key { get; init; }
    public required string Label { get; init; }
}
