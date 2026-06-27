using CanXe.Application.Interfaces;
using CanXe.Application.Mapping;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public sealed class WeighTicketService
{
    private readonly IWeighTicketRepository _ticketRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICargoTypeRepository _cargoTypeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IScaleService _scaleService;
    private readonly ICameraService _cameraService;
    private readonly IUserNotificationService? _notificationService;

    public WeighTicketService(
        IWeighTicketRepository ticketRepository,
        ICustomerRepository customerRepository,
        ICargoTypeRepository cargoTypeRepository,
        IVehicleRepository vehicleRepository,
        IScaleService scaleService,
        ICameraService cameraService,
        IUserNotificationService? notificationService = null)
    {
        _ticketRepository = ticketRepository;
        _customerRepository = customerRepository;
        _cargoTypeRepository = cargoTypeRepository;
        _vehicleRepository = vehicleRepository;
        _scaleService = scaleService;
        _cameraService = cameraService;
        _notificationService = notificationService;
    }

    public async Task<WeighTicketDraft> LoadTicketForContinuationAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy phiếu #{ticketId}.");

        if (ticket.Events.Count >= 2)
            throw new InvalidOperationException("Phiếu đã có đủ hai lần cân.");

        var draft = MapToDraft(ticket);
        draft.ExistingTicketId = ticket.Id;
        return draft;
    }

    public async Task<RecordWeightResult> RecordWeightAsync(
        WeighTicketDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (draft.Events.Count >= 2)
            return new RecordWeightResult
            {
                Success = false,
                ErrorMessage = "Đã ghi đủ hai trọng lượng.",
                IsRecordingLocked = true
            };

        var sequence = draft.Events.Count + 1;
        var weightKg = Math.Round(await _scaleService.GetCurrentWeightAsync(cancellationToken), 0, MidpointRounding.AwayFromZero);
        var recordedAt = DateTimeOffset.Now;

        var weighEvent = new WeighEventDraft
        {
            Sequence = sequence,
            WeightKg = weightKg,
            RecordedAt = recordedAt
        };

        draft.Events.Add(weighEvent);

        if (draft.ExistingTicketId is int ticketId)
        {
            var persisted = await PersistEventForExistingTicketAsync(
                ticketId, draft, weighEvent, cancellationToken);
            weighEvent.PersistedEventId = persisted.Id;
        }

        _ = CapturePhotoInBackgroundAsync(draft, weighEvent);

        return new RecordWeightResult
        {
            Success = true,
            Event = weighEvent,
            IsRecordingLocked = draft.Events.Count >= 2
        };
    }

    public async Task<SaveTicketResult> SaveAsync(
        WeighTicketDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (draft.Events.Count == 0)
            return new SaveTicketResult { Success = false, ErrorMessage = "Cần ít nhất một trọng lượng để lưu." };

        var similarWarnings = await GetSimilarCustomerWarningsAsync(draft, cancellationToken);

        try
        {
            WeighTicket ticket;
            if (draft.ExistingTicketId is int existingId)
            {
                ticket = await CompleteExistingTicketAsync(existingId, draft, cancellationToken);
            }
            else
            {
                ticket = await CreateNewTicketAsync(draft, cancellationToken);
            }

            var listItem = MapToListItem(ticket);
            return new SaveTicketResult
            {
                Success = true,
                SavedTicket = listItem,
                SimilarCustomerWarnings = similarWarnings
            };
        }
        catch (Exception ex)
        {
            return new SaveTicketResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public Task<IReadOnlyList<WeighTicketListItem>> GetFilteredAsync(
        WeighTicketFilter filter,
        CancellationToken cancellationToken = default)
    {
        return GetFilteredListItemsAsync(filter, cancellationToken);
    }

    public Task<IReadOnlyList<Customer>> SearchCustomersAsync(
        string searchTerm,
        CancellationToken cancellationToken = default) =>
        _customerRepository.SearchAsync(searchTerm, 10, cancellationToken);

    public Task<IReadOnlyList<CargoType>> SearchCargoTypesAsync(
        string searchTerm,
        CancellationToken cancellationToken = default) =>
        _cargoTypeRepository.SearchAsync(searchTerm, 10, cancellationToken);

    private async Task<WeighTicket> CompleteExistingTicketAsync(
        int ticketId,
        WeighTicketDraft draft,
        CancellationToken cancellationToken)
    {
        var ticket = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy phiếu #{ticketId}.");

        await ApplyDraftMetadataAsync(ticket, draft, cancellationToken);

        foreach (var eventDraft in draft.Events.Where(e => e.PersistedEventId is null))
        {
            var entity = new WeighEvent
            {
                WeighTicketId = ticket.Id,
                Sequence = eventDraft.Sequence,
                WeightGrams = WeightStorageMapper.ToGrams(eventDraft.WeightKg)!.Value,
                RecordedAt = eventDraft.RecordedAt,
                PhotoPath = eventDraft.PhotoPath,
                PhotoCaptureSucceeded = eventDraft.PhotoCaptureSucceeded,
                PhotoErrorMessage = eventDraft.PhotoErrorMessage
            };
            var saved = await _ticketRepository.AddEventAsync(entity, cancellationToken);
            eventDraft.PersistedEventId = saved.Id;
        }

        foreach (var eventDraft in draft.Events.Where(e => e.PersistedEventId is not null))
        {
            var existing = ticket.Events.FirstOrDefault(e => e.Id == eventDraft.PersistedEventId);
            if (existing is null)
                continue;

            existing.PhotoPath = eventDraft.PhotoPath;
            existing.PhotoCaptureSucceeded = eventDraft.PhotoCaptureSucceeded;
            existing.PhotoErrorMessage = eventDraft.PhotoErrorMessage;
            await _ticketRepository.UpdateEventAsync(existing, cancellationToken);
        }

        ApplyCalculations(ticket, draft);
        ticket.UpdatedAt = DateTimeOffset.Now;
        await _ticketRepository.UpdateAsync(ticket, cancellationToken);

        return (await _ticketRepository.GetByIdWithEventsAsync(ticket.Id, cancellationToken))!;
    }

    private async Task<WeighTicket> CreateNewTicketAsync(
        WeighTicketDraft draft,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var sequence = await _ticketRepository.GetNextSequenceAsync(now.Year, now.Month, cancellationToken);

        var ticket = new WeighTicket
        {
            SequenceNumber = sequence,
            TicketYear = now.Year,
            TicketMonth = now.Month,
            InternalCode = TicketNumberFormatter.FormatInternalCode(now.Year, now.Month, sequence),
            DisplayNumber = TicketNumberFormatter.FormatDisplayNumber(sequence, now.Month),
            TicketDateTime = now,
            CreatedAt = now
        };

        await ApplyDraftMetadataAsync(ticket, draft, cancellationToken);

        foreach (var eventDraft in draft.Events)
        {
            ticket.Events.Add(new WeighEvent
            {
                Sequence = eventDraft.Sequence,
                WeightGrams = WeightStorageMapper.ToGrams(eventDraft.WeightKg)!.Value,
                RecordedAt = eventDraft.RecordedAt,
                PhotoPath = eventDraft.PhotoPath,
                PhotoCaptureSucceeded = eventDraft.PhotoCaptureSucceeded,
                PhotoErrorMessage = eventDraft.PhotoErrorMessage
            });
        }

        ApplyCalculations(ticket, draft);

        return await _ticketRepository.AddAsync(ticket, cancellationToken);
    }

    private async Task ApplyDraftMetadataAsync(
        WeighTicket ticket,
        WeighTicketDraft draft,
        CancellationToken cancellationToken)
    {
        ticket.Notes = NullIfWhiteSpace(draft.Notes);
        ticket.UnitPriceVndPerKg = WeightStorageMapper.ToVndPerKg(draft.UnitPriceVndPerKg);

        if (NullIfWhiteSpace(draft.CustomerName) is { } customerName)
        {
            var customer = await _customerRepository.UpsertAsync(customerName, cancellationToken);
            ticket.CustomerId = customer.Id;
            ticket.CustomerNameSnapshot = customer.Name;
        }
        else
        {
            ticket.CustomerId = null;
            ticket.CustomerNameSnapshot = null;
        }

        if (NullIfWhiteSpace(draft.CargoTypeName) is { } cargoTypeName)
        {
            var cargoType = await _cargoTypeRepository.UpsertAsync(cargoTypeName, cancellationToken);
            ticket.CargoTypeId = cargoType.Id;
            ticket.CargoTypeNameSnapshot = cargoType.Name;
        }
        else
        {
            ticket.CargoTypeId = null;
            ticket.CargoTypeNameSnapshot = null;
        }

        if (NullIfWhiteSpace(draft.LicensePlate) is { } plate)
        {
            var vehicle = await _vehicleRepository.UpsertAsync(plate, ticket.CustomerId, cancellationToken);
            ticket.VehicleId = vehicle.Id;
            ticket.LicensePlateSnapshot = vehicle.PlateNumber;
        }
        else
        {
            ticket.VehicleId = null;
            ticket.LicensePlateSnapshot = null;
        }
    }

    private static void ApplyCalculations(WeighTicket ticket, WeighTicketDraft draft)
    {
        var w1 = draft.Events.FirstOrDefault(e => e.Sequence == 1)?.WeightKg;
        var w2 = draft.Events.FirstOrDefault(e => e.Sequence == 2)?.WeightKg;
        WeightStorageMapper.ApplyCalculationToTicket(ticket, w1, w2, draft.UnitPriceVndPerKg);
    }

    private async Task<WeighEvent> PersistEventForExistingTicketAsync(
        int ticketId,
        WeighTicketDraft draft,
        WeighEventDraft eventDraft,
        CancellationToken cancellationToken)
    {
        var entity = new WeighEvent
        {
            WeighTicketId = ticketId,
            Sequence = eventDraft.Sequence,
            WeightGrams = WeightStorageMapper.ToGrams(eventDraft.WeightKg)!.Value,
            RecordedAt = eventDraft.RecordedAt,
            PhotoCaptureSucceeded = false
        };

        var saved = await _ticketRepository.AddEventAsync(entity, cancellationToken);

        var ticket = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy phiếu #{ticketId}.");
        ApplyCalculations(ticket, draft);
        ticket.UpdatedAt = DateTimeOffset.Now;
        await _ticketRepository.UpdateAsync(ticket, cancellationToken);

        return saved;
    }

    private async Task CapturePhotoInBackgroundAsync(WeighTicketDraft draft, WeighEventDraft eventDraft)
    {
        try
        {
            var code = draft.InternalCode ?? draft.ExistingTicketId?.ToString() ?? "DRAFT";
            var result = await _cameraService.CaptureAsync(code, eventDraft.Sequence);

            eventDraft.PhotoCaptureSucceeded = result.Success;
            eventDraft.PhotoPath = result.FilePath;
            eventDraft.PhotoErrorMessage = result.ErrorMessage;

            if (eventDraft.PersistedEventId is int eventId)
            {
                var ticket = await _ticketRepository.GetByIdWithEventsAsync(draft.ExistingTicketId!.Value);
                var entity = ticket?.Events.FirstOrDefault(e => e.Id == eventId);
                if (entity is not null)
                {
                    entity.PhotoPath = result.FilePath;
                    entity.PhotoCaptureSucceeded = result.Success;
                    entity.PhotoErrorMessage = result.ErrorMessage;
                    await _ticketRepository.UpdateEventAsync(entity);
                }
            }
        }
        catch (Exception ex)
        {
            eventDraft.PhotoCaptureSucceeded = false;
            eventDraft.PhotoErrorMessage = ex.Message;
            _notificationService?.Notify($"Camera lỗi: {ex.Message}");
        }

        if (!eventDraft.PhotoCaptureSucceeded && !string.IsNullOrEmpty(eventDraft.PhotoErrorMessage))
            _notificationService?.Notify($"Camera lỗi: {eventDraft.PhotoErrorMessage}");
    }

    private async Task<IReadOnlyList<string>> GetSimilarCustomerWarningsAsync(
        WeighTicketDraft draft,
        CancellationToken cancellationToken)
    {
        if (NullIfWhiteSpace(draft.CustomerName) is not { } name)
            return [];

        var similar = await _customerRepository.FindSimilarAsync(name, 5, cancellationToken);
        return similar
            .Where(c => !string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(c => $"Tên khách '{name}' gần giống '{c.Name}'.")
            .ToList();
    }

    private async Task<IReadOnlyList<WeighTicketListItem>> GetFilteredListItemsAsync(
        WeighTicketFilter filter,
        CancellationToken cancellationToken)
    {
        var tickets = await _ticketRepository.GetFilteredAsync(filter, cancellationToken);
        return tickets.Select(MapToListItem).ToList();
    }

    private static WeighTicketDraft MapToDraft(WeighTicket ticket)
    {
        var draft = new WeighTicketDraft
        {
            ExistingTicketId = ticket.Id,
            DisplayNumber = ticket.DisplayNumber,
            InternalCode = ticket.InternalCode,
            TicketDateTime = ticket.TicketDateTime,
            CustomerName = ticket.CustomerNameSnapshot,
            LicensePlate = ticket.LicensePlateSnapshot,
            CargoTypeName = ticket.CargoTypeNameSnapshot,
            UnitPriceVndPerKg = WeightStorageMapper.FromVndPerKg(ticket.UnitPriceVndPerKg),
            Notes = ticket.Notes
        };

        foreach (var e in ticket.Events.OrderBy(x => x.Sequence))
        {
            draft.Events.Add(new WeighEventDraft
            {
                Sequence = e.Sequence,
                WeightKg = WeightStorageMapper.FromGrams(e.WeightGrams)!.Value,
                RecordedAt = e.RecordedAt,
                PersistedEventId = e.Id,
                PhotoPath = e.PhotoPath,
                PhotoCaptureSucceeded = e.PhotoCaptureSucceeded,
                PhotoErrorMessage = e.PhotoErrorMessage
            });
        }

        return draft;
    }

    private static WeighTicketListItem MapToListItem(WeighTicket ticket)
    {
        var w1 = ticket.Events.FirstOrDefault(e => e.Sequence == 1);
        var w2 = ticket.Events.FirstOrDefault(e => e.Sequence == 2);

        return new WeighTicketListItem
        {
            Id = ticket.Id,
            TicketDateTime = ticket.TicketDateTime,
            DisplayNumber = ticket.DisplayNumber,
            LicensePlate = ticket.LicensePlateSnapshot,
            CustomerName = ticket.CustomerNameSnapshot,
            CargoTypeName = ticket.CargoTypeNameSnapshot,
            Weight1Kg = WeightStorageMapper.FromGrams(w1?.WeightGrams),
            Weight2Kg = WeightStorageMapper.FromGrams(w2?.WeightGrams),
            GrossWeightKg = WeightStorageMapper.FromGrams(ticket.GrossWeightGrams),
            TareWeightKg = WeightStorageMapper.FromGrams(ticket.TareWeightGrams),
            NetWeightKg = WeightStorageMapper.FromGrams(ticket.NetWeightGrams),
            BillableWeightKg = WeightStorageMapper.FromGrams(ticket.BillableWeightGrams),
            UnitPriceVndPerKg = WeightStorageMapper.FromVndPerKg(ticket.UnitPriceVndPerKg),
            TotalAmountVnd = WeightStorageMapper.FromVnd(ticket.TotalAmountVnd),
            Notes = ticket.Notes,
            EventCount = ticket.Events.Count
        };
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
