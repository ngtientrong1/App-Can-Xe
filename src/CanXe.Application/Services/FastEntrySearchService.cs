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

        var items = ranked.Select(v => new AutocompleteSuggestionItem
        {
            PrimaryText = v.PlateNumber,
            SecondaryText = v.LastCustomer?.Name is { } name ? $"Khách gần nhất: {name}" : null,
            Tag = v
        }).ToList();

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
