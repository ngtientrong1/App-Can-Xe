namespace CanXe.Domain.Services;

public readonly record struct CargoUsageTicketRow(
    int? CargoTypeId,
    string? CargoTypeName,
    DateTimeOffset TicketDateTime);

public readonly record struct FrequentCargoTypeResult(
    int? CargoTypeId,
    string CargoTypeName,
    int UsageCount,
    DateTimeOffset LastUsedAt);

public static class FrequentCargoTypeResolver
{
    public const int MaxTicketsToAnalyze = 50;

    public static FrequentCargoTypeResult? Resolve(IReadOnlyList<CargoUsageTicketRow> tickets)
    {
        var valid = tickets
            .Where(t => t.CargoTypeId.HasValue || !string.IsNullOrWhiteSpace(t.CargoTypeName))
            .ToList();

        if (valid.Count == 0)
            return null;

        var grouped = valid
            .GroupBy(t => (
                Id: t.CargoTypeId,
                Key: t.CargoTypeId.HasValue
                    ? $"id:{t.CargoTypeId.Value}"
                    : $"name:{TextNormalizer.Normalize(t.CargoTypeName)}"))
            .Select(g =>
            {
                var sample = g.OrderByDescending(x => x.TicketDateTime).First();
                return new
                {
                    sample.CargoTypeId,
                    Name = sample.CargoTypeName!.Trim(),
                    Count = g.Count(),
                    LastUsed = g.Max(x => x.TicketDateTime)
                };
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.LastUsed)
            .First();

        return new FrequentCargoTypeResult(
            grouped.CargoTypeId,
            grouped.Name,
            grouped.Count,
            grouped.LastUsed);
    }
}
