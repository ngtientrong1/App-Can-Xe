using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public sealed class FastEntrySearchService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICargoTypeRepository _cargoTypeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IWeighTicketRepository _ticketRepository;

    public FastEntrySearchService(
        ICustomerRepository customerRepository,
        ICargoTypeRepository cargoTypeRepository,
        IVehicleRepository vehicleRepository,
        IWeighTicketRepository ticketRepository)
    {
        _customerRepository = customerRepository;
        _cargoTypeRepository = cargoTypeRepository;
        _vehicleRepository = vehicleRepository;
        _ticketRepository = ticketRepository;
    }

    public async Task<IReadOnlyList<AutocompleteSuggestionItem>> SearchCustomersAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var customers = await _customerRepository.SearchAsync(searchTerm, 20, cancellationToken);
        var ranked = AutocompleteRanker.Rank(
            customers,
            c => c.Name,
            c => c.LastUsedAt,
            searchTerm,
            8);

        var items = new List<AutocompleteSuggestionItem>();
        foreach (var customer in ranked)
        {
            var plate = await _ticketRepository.GetLatestPlateForCustomerAsync(customer.Name, cancellationToken);
            var secondary = plate is not null
                ? $"Xe gần nhất: {plate} · Dùng gần đây"
                : "Dùng gần đây";

            items.Add(new AutocompleteSuggestionItem
            {
                PrimaryText = customer.Name,
                SecondaryText = secondary,
                Tag = customer
            });
        }

        AppendNewEntryOption(items, searchTerm);
        return items;
    }

    public async Task<IReadOnlyList<AutocompleteSuggestionItem>> SearchVehiclesAsync(
        string searchTerm,
        string? preferredCustomerName,
        CancellationToken cancellationToken = default)
    {
        var vehicles = await _vehicleRepository.SearchAsync(searchTerm, 20, cancellationToken);
        var ranked = vehicles
            .Select(v => (
                Vehicle: v,
                Score: AutocompleteRanker.ScoreMatch(v.PlateNumber, searchTerm) +
                       (preferredCustomerName is not null &&
                        string.Equals(v.LastCustomer?.Name, preferredCustomerName, StringComparison.OrdinalIgnoreCase)
                           ? 50 : 0)))
            .Where(x => string.IsNullOrWhiteSpace(searchTerm) || x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Vehicle.LastUsedAt)
            .Take(8)
            .Select(x => x.Vehicle)
            .ToList();

        var items = new List<AutocompleteSuggestionItem>();
        foreach (var vehicle in ranked)
        {
            var context = await _ticketRepository.GetVehicleUsageContextAsync(
                PlateNormalizer.Normalize(vehicle.PlateNumber),
                cancellationToken);

            string? secondary = null;
            string? tertiary = null;
            if (context?.RecentCustomerName is { } customerName)
                secondary = $"Khách gần nhất: {customerName}";

            var cargoName = context?.FrequentCargoTypeName ?? context?.RecentCargoTypeName;
            if (cargoName is not null)
                tertiary = $"Loại hàng thường dùng: {cargoName}";

            items.Add(new AutocompleteSuggestionItem
            {
                PrimaryText = vehicle.PlateNumber ?? string.Empty,
                SecondaryText = secondary,
                TertiaryText = tertiary,
                Tag = vehicle
            });
        }

        AppendNewEntryOption(items, searchTerm);
        return items;
    }

    public async Task<IReadOnlyList<AutocompleteSuggestionItem>> SearchCargoTypesAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var cargoTypes = await _cargoTypeRepository.SearchAsync(searchTerm, 20, cancellationToken);
        var ranked = AutocompleteRanker.Rank(
            cargoTypes,
            c => c.Name,
            _ => null,
            searchTerm,
            8);

        var items = new List<AutocompleteSuggestionItem>();
        foreach (var cargo in ranked)
        {
            var count = await _ticketRepository.GetCargoUsageCountAsync(cargo.Name, cancellationToken);
            items.Add(new AutocompleteSuggestionItem
            {
                PrimaryText = cargo.Name,
                SecondaryText = count > 0 ? $"Dùng {count} lần gần đây" : null,
                Tag = cargo
            });
        }

        AppendNewEntryOption(items, searchTerm);
        return items;
    }

    public Task<IReadOnlyList<AutocompleteSuggestionItem>> SearchNotesAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Task.FromResult<IReadOnlyList<AutocompleteSuggestionItem>>([]);

        return SearchNotesInternalAsync(searchTerm, cancellationToken);
    }

    private async Task<IReadOnlyList<AutocompleteSuggestionItem>> SearchNotesInternalAsync(
        string searchTerm,
        CancellationToken cancellationToken)
    {
        var notes = await _ticketRepository.SearchRecentNotesAsync(searchTerm, 5, cancellationToken);
        return notes.Select(n => new AutocompleteSuggestionItem { PrimaryText = n }).ToList();
    }

    public async Task<IReadOnlyList<AutocompleteSuggestionItem>> GetCustomerHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var catalog = await _customerRepository.ListActiveNamesForHistoryAsync(50, cancellationToken);
        if (catalog.Count > 0)
            return BuildHistoryItems(catalog);

        var recent = await _ticketRepository.GetRecentCustomerNamesAsync(50, cancellationToken);
        return await BuildActiveCatalogHistoryItemsAsync(
            recent,
            name => _customerRepository.FindActiveByNormalizedNameAsync(TextNormalizer.Normalize(name), cancellationToken));
    }

    public async Task<IReadOnlyList<AutocompleteSuggestionItem>> GetVehicleHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var catalog = await _vehicleRepository.ListActivePlatesForHistoryAsync(50, cancellationToken);
        if (catalog.Count > 0)
            return BuildHistoryItems(catalog);

        var recent = await _ticketRepository.GetRecentLicensePlatesAsync(50, cancellationToken);
        return await BuildActiveCatalogHistoryItemsAsync(
            recent,
            plate => _vehicleRepository.FindActiveByPlateAsync(plate, cancellationToken));
    }

    public async Task<IReadOnlyList<AutocompleteSuggestionItem>> GetCargoTypeHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var catalog = await _cargoTypeRepository.ListActiveNamesForHistoryAsync(50, cancellationToken);
        if (catalog.Count > 0)
            return BuildHistoryItems(catalog);

        var recent = await _ticketRepository.GetRecentCargoTypeNamesAsync(50, cancellationToken);
        return await BuildActiveCatalogHistoryItemsAsync(
            recent,
            name => _cargoTypeRepository.FindActiveByNormalizedNameAsync(TextNormalizer.Normalize(name), cancellationToken));
    }

    public async Task<decimal?> GetCargoDefaultUnitPriceAsync(
        string cargoName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cargoName))
            return null;

        var normalized = TextNormalizer.Normalize(cargoName.Trim());
        var cargo = await _cargoTypeRepository.FindActiveByNormalizedNameAsync(normalized, cancellationToken);
        if (cargo?.DefaultUnitPriceVndPerKg is int catalogPrice and > 0)
            return catalogPrice;

        return await _ticketRepository.GetRecentUnitPriceForCargoAsync(cargoName, cancellationToken);
    }

    public Task<decimal?> GetRecentUnitPriceForCargoAsync(
        string cargoTypeName,
        CancellationToken cancellationToken = default) =>
        GetCargoDefaultUnitPriceAsync(cargoTypeName, cancellationToken);

    private static IReadOnlyList<AutocompleteSuggestionItem> BuildHistoryItems(IReadOnlyList<string> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => new AutocompleteSuggestionItem
            {
                PrimaryText = v,
                SecondaryText = "Danh mục"
            })
            .ToList();

    private static async Task<IReadOnlyList<AutocompleteSuggestionItem>> BuildActiveCatalogHistoryItemsAsync<T>(
        IReadOnlyList<string> values,
        Func<string, Task<T?>> findActive)
        where T : class
    {
        var activeValues = new List<string>();
        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            if (await findActive(value.Trim()) is not null)
                activeValues.Add(value.Trim());
        }

        return BuildHistoryItems(activeValues);
    }

    private static void AppendNewEntryOption(List<AutocompleteSuggestionItem> items, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return;

        items.Add(new AutocompleteSuggestionItem
        {
            PrimaryText = $"+ Dùng tên mới: \"{searchTerm.Trim()}\"",
            IsNewEntryOption = true
        });
    }
}
