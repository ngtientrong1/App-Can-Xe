using CanXe.Application.Interfaces;
using CanXe.Application.Mapping;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
using CanXe.Domain.Models;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public sealed class WeighTicketService
{
    private readonly IWeighTicketRepository _ticketRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICargoTypeRepository _cargoTypeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IScaleService _scaleService;
    private readonly IUserNotificationService? _notificationService;
    private readonly TicketUpdateService _ticketUpdateService;

    public Task WaitForPendingPhotosAsync() => Task.CompletedTask;

    public WeighTicketService(
        IWeighTicketRepository ticketRepository,
        ICustomerRepository customerRepository,
        ICargoTypeRepository cargoTypeRepository,
        IVehicleRepository vehicleRepository,
        IScaleService scaleService,
        TicketUpdateService ticketUpdateService,
        IUserNotificationService? notificationService = null)
    {
        _ticketRepository = ticketRepository;
        _customerRepository = customerRepository;
        _cargoTypeRepository = cargoTypeRepository;
        _vehicleRepository = vehicleRepository;
        _scaleService = scaleService;
        _ticketUpdateService = ticketUpdateService;
        _notificationService = notificationService;
    }

    public Task<WeighTicketDraft> LoadTicketForEditAsync(
        int ticketId,
        CancellationToken cancellationToken = default) =>
        _ticketUpdateService.LoadForEditAsync(ticketId, cancellationToken);

    public Task<WeighTicketDraft> LoadTicketForViewAsync(
        int ticketId,
        CancellationToken cancellationToken = default) =>
        _ticketUpdateService.LoadForViewAsync(ticketId, cancellationToken);

    public Task<UpdateTicketResult> UpdateTicketAsync(
        WeighTicketDraft draft,
        string? editedBy = null,
        CancellationToken cancellationToken = default) =>
        _ticketUpdateService.UpdateAsync(draft, editedBy, cancellationToken);

    public static bool TicketMatchesFilter(WeighTicket ticket, WeighTicketFilter filter) =>
        TicketUpdateService.MatchesFilterInternal(ticket, filter);

    public async Task<WeighTicketDraft> LoadTicketForContinuationAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy phiếu #{ticketId}.");

        if (ticket.Events.Count >= 2)
            throw new InvalidOperationException("Phiếu đã có đủ hai lần cân.");

        var draft = new WeighTicketDraft
        {
            ExistingTicketId = ticket.Id,
            DisplayNumber = TicketNumberFormatter.ResolveDisplayNumber(
                ticket.SequenceNumber, ticket.TicketMonth, ticket.DisplayNumber),
            InternalCode = ticket.InternalCode,
            TicketDateTime = ticket.TicketDateTime,
            DraftCustomer = ticket.CustomerNameSnapshot,
            DraftVehicle = ticket.LicensePlateSnapshot,
            DraftCargoType = ticket.CargoTypeNameSnapshot,
            DraftUnitPrice = WeightStorageMapper.FromVndPerKg(ticket.UnitPriceVndPerKg),
            DraftNotes = ticket.Notes
        };

        var w1 = ticket.Events.FirstOrDefault(e => e.Sequence == 1);
        if (w1 is not null)
        {
            draft.DraftWeight1 = WeightStorageMapper.FromGrams(w1 is null ? null : WeighEventWeightResolver.GetEffectiveWeightGrams(w1));
            draft.DraftWeight1RecordedAt = w1.RecordedAt;
            draft.DraftWeight1PhotoPath = w1.PhotoPath;
            draft.DraftWeight1PhotoStatus = w1.PhotoCaptureSucceeded ? DraftPhotoStatus.Valid : DraftPhotoStatus.Failed;
            draft.IsWeight1LockedFromSavedTicket = true;
            draft.SavedWeight1EventId = w1.Id;
        }

        var w2 = ticket.Events.FirstOrDefault(e => e.Sequence == 2);
        if (w2 is not null)
        {
            draft.DraftWeight2 = WeightStorageMapper.FromGrams(w2 is null ? null : WeighEventWeightResolver.GetEffectiveWeightGrams(w2));
            draft.DraftWeight2RecordedAt = w2.RecordedAt;
            draft.DraftWeight2PhotoPath = w2.PhotoPath;
            draft.DraftWeight2PhotoStatus = w2.PhotoCaptureSucceeded ? DraftPhotoStatus.Valid : DraftPhotoStatus.Failed;
            draft.IsWeight2LockedFromSavedTicket = true;
            draft.SavedWeight2EventId = w2.Id;
        }

        return draft;
    }

    public Task<CaptureWeightResult> CaptureWeightAsync(
        WeighTicketDraft draft,
        int sequence,
        CancellationToken cancellationToken = default)
    {
        if (sequence is not (1 or 2))
            return Task.FromResult(new CaptureWeightResult { Success = false, ErrorMessage = "Lần cân không hợp lệ." });

        if (sequence == 1 && !DraftWorkflowRules.CanUpdateWeight1(
                draft.IsWeight1LockedFromSavedTicket,
                draft.DraftWeight2.HasValue,
                draft.DeveloperWeight1OverrideEnabled))
            return Task.FromResult(new CaptureWeightResult { Success = false, ErrorMessage = "Cân lần 1 đã khóa sau khi có cân lần 2." });

        if (sequence == 1 && draft.IsWeight1LockedFromSavedTicket)
            return Task.FromResult(new CaptureWeightResult { Success = false, ErrorMessage = "Không thể sửa cân lần 1 đã lưu." });

        if (sequence == 2 && draft.IsWeight2LockedFromSavedTicket)
            return Task.FromResult(new CaptureWeightResult { Success = false, ErrorMessage = "Không thể sửa cân lần 2 đã lưu." });

        var isUpdate = sequence == 1 ? draft.DraftWeight1.HasValue : draft.DraftWeight2.HasValue;

        if (_scaleService is IHardwareScaleDiagnostics hardware && hardware.InputMode == ScaleInputMode.Hardware)
        {
            if (!hardware.CanCaptureWeight())
            {
                return Task.FromResult(new CaptureWeightResult
                {
                    Success = false,
                    ErrorMessage = hardware.GetHardwareCaptureBlockReason() ?? "Trọng lượng chưa ổn định"
                });
            }
        }

        try
        {
            var weightKg = ReadCurrentWeightKg();
            var recordedAt = DateTimeOffset.Now;
            draft.SetWeightDraft(sequence, weightKg, recordedAt);
            return Task.FromResult(new CaptureWeightResult
            {
                Success = true,
                Sequence = sequence,
                WeightKg = weightKg,
                IsUpdate = isUpdate
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new CaptureWeightResult { Success = false, ErrorMessage = ex.Message });
        }
    }

    private decimal ReadCurrentWeightKg()
    {
        if (_scaleService is IHardwareScaleDiagnostics hardware && hardware.InputMode == ScaleInputMode.Hardware)
        {
            var reading = hardware.LatestReading;
            if (reading is null)
                throw new InvalidOperationException("Đầu cân COM chưa sẵn sàng.");
            return Math.Round((decimal)reading.WeightKg, 0, MidpointRounding.AwayFromZero);
        }

        return Math.Round(_scaleService.GetCurrentWeightAsync().GetAwaiter().GetResult(), 0, MidpointRounding.AwayFromZero);
    }

    public async Task<SaveTicketResult> SaveAsync(
        WeighTicketDraft draft,
        WeighTicketFilter? visibilityFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (!draft.HasAnyWeight)
            return new SaveTicketResult { Success = false, ErrorMessage = "Cần ít nhất một trọng lượng để lưu." };

        var similarWarnings = await GetSimilarCustomerWarningsAsync(draft, cancellationToken);

        try
        {
            WeighTicket ticket = draft.ExistingTicketId is int existingId
                ? await SaveContinuationAsync(existingId, draft, cancellationToken)
                : await SaveNewTicketAsync(draft, cancellationToken);

            return new SaveTicketResult
            {
                Success = true,
                SavedTicket = MapToListItem(ticket),
                WorkflowState = WeighTicketWorkflow.FromEventCount(ticket.Events.Count),
                SimilarCustomerWarnings = similarWarnings,
                IsVisibleInCurrentFilter = visibilityFilter is null ||
                    TicketUpdateService.TicketMatchesFilter(ticket, visibilityFilter)
            };
        }
        catch (Exception ex)
        {
            return new SaveTicketResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public void CancelDraft(WeighTicketDraft draft)
    {
        // Draft session cleanup only — legacy photo files are not managed in Phase 4.
    }

    public Task<IReadOnlyList<WeighTicketListItem>> GetFilteredAsync(
        WeighTicketFilter filter,
        CancellationToken cancellationToken = default) =>
        GetFilteredListItemsAsync(filter, cancellationToken);

    public async Task<WeighTicketFilterResult> GetFilteredWithSummaryAsync(
        WeighTicketFilter filter,
        CancellationToken cancellationToken = default)
    {
        var items = await GetFilteredListItemsAsync(filter, cancellationToken);
        return FilterSummaryCalculator.Build(items);
    }

    public Task<IReadOnlyList<Customer>> SearchCustomersAsync(
        string searchTerm,
        CancellationToken cancellationToken = default) =>
        _customerRepository.SearchAsync(searchTerm, 10, cancellationToken);

    public Task<IReadOnlyList<CargoType>> SearchCargoTypesAsync(
        string searchTerm,
        CancellationToken cancellationToken = default) =>
        _cargoTypeRepository.SearchAsync(searchTerm, 10, cancellationToken);

    public Task<VehicleUsageContext?> GetVehicleUsageContextAsync(
        string plateNumber,
        CancellationToken cancellationToken = default) =>
        _ticketRepository.GetVehicleUsageContextAsync(
            PlateNormalizer.Normalize(plateNumber),
            cancellationToken);

    public Task<IReadOnlyList<Vehicle>> SearchVehiclesAsync(
        string searchTerm,
        CancellationToken cancellationToken = default) =>
        _vehicleRepository.SearchAsync(searchTerm, 10, cancellationToken);

    public async Task<string> GetPreviewDisplayNumberAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        var sequence = await _ticketRepository.PeekNextSequenceAsync(now.Year, now.Month, cancellationToken);
        return TicketNumberFormatter.FormatDisplayNumber(sequence, now.Month);
    }

    public async Task<WeighTicketDetailDto> GetTicketDetailAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy phiếu #{ticketId}.");

        return MapToDetail(ticket);
    }

    private static WeighTicketDetailDto MapToDetail(WeighTicket ticket)
    {
        var w1 = ticket.Events.FirstOrDefault(e => e.Sequence == 1);
        var w2 = ticket.Events.FirstOrDefault(e => e.Sequence == 2);
        var unitPrice = WeightStorageMapper.FromVndPerKg(ticket.UnitPriceVndPerKg);
        var weight1 = w1 is null ? null : WeightStorageMapper.FromGrams(WeighEventWeightResolver.GetEffectiveWeightGrams(w1));
        var weight2 = w2 is null ? null : WeightStorageMapper.FromGrams(WeighEventWeightResolver.GetEffectiveWeightGrams(w2));

        return new WeighTicketDetailDto
        {
            Id = ticket.Id,
            TicketDateTime = ticket.TicketDateTime,
            DisplayNumber = TicketNumberFormatter.ResolveDisplayNumber(
                ticket.SequenceNumber, ticket.TicketMonth, ticket.DisplayNumber),
            LicensePlate = ticket.LicensePlateSnapshot,
            CustomerName = ticket.CustomerNameSnapshot,
            CargoTypeName = ticket.CargoTypeNameSnapshot,
            Notes = ticket.Notes,
            UnitPriceVndPerKg = unitPrice,
            IsServiceWeigh = !WeightCalculator.HasBillableUnitPrice(unitPrice),
            Weight1Kg = weight1,
            Weight1RecordedAt = w1?.RecordedAt,
            Weight1PhotoPath = w1?.PhotoPath,
            Weight1PhotoAvailable = false,
            Weight1PhotoStatusText = null,
            Weight2Kg = weight2,
            Weight2RecordedAt = w2?.RecordedAt,
            Weight2PhotoPath = w2?.PhotoPath,
            Weight2PhotoAvailable = false,
            Weight2PhotoStatusText = null,
            GrossWeightKg = WeightStorageMapper.FromGrams(ticket.GrossWeightGrams),
            TareWeightKg = WeightStorageMapper.FromGrams(ticket.TareWeightGrams),
            NetWeightKg = WeightStorageMapper.FromGrams(ticket.NetWeightGrams),
            DeductionWeightKg = WeightStorageMapper.FromGrams(ticket.DeductionWeightGrams),
            BillableWeightKg = WeightStorageMapper.FromGrams(ticket.BillableWeightGrams),
            TotalAmountVnd = WeightStorageMapper.FromVnd(ticket.TotalAmountVnd),
            IsSingleWeigh = WeightCalculator.IsSingleWeigh(weight1, weight2)
        };
    }

    private async Task<WeighTicket> SaveNewTicketAsync(
        WeighTicketDraft draft,
        CancellationToken cancellationToken)
    {
        return await _ticketRepository.ExecuteInTransactionAsync(async () =>
        {
            var now = DateTimeOffset.Now;
            var sequence = await _ticketRepository.GetNextSequenceAsync(now.Year, now.Month, cancellationToken);
            var internalCode = TicketNumberFormatter.FormatInternalCode(now.Year, now.Month, sequence);

            var ticket = new WeighTicket
            {
                SequenceNumber = sequence,
                TicketYear = now.Year,
                TicketMonth = now.Month,
                InternalCode = internalCode,
                DisplayNumber = TicketNumberFormatter.FormatDisplayNumber(sequence, now.Month),
                TicketDateTime = now,
                CreatedAt = now
            };

            await ApplyDraftMetadataAsync(ticket, draft, cancellationToken);
            AddDraftEventsToTicket(ticket, draft, internalCode);

            ApplyCalculations(ticket, draft);
            return await _ticketRepository.AddAsync(ticket, cancellationToken);
        }, cancellationToken);
    }

    private async Task<WeighTicket> SaveContinuationAsync(
        int ticketId,
        WeighTicketDraft draft,
        CancellationToken cancellationToken)
    {
        return await _ticketRepository.ExecuteInTransactionAsync(async () =>
        {
            var ticket = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken)
                ?? throw new InvalidOperationException($"Không tìm thấy phiếu #{ticketId}.");

            await ApplyDraftMetadataAsync(ticket, draft, cancellationToken);

            if (draft.DraftWeight1.HasValue && !draft.IsWeight1LockedFromSavedTicket &&
                ticket.Events.All(e => e.Sequence != 1))
            {
                await _ticketRepository.AddEventAsync(new WeighEvent
                {
                    WeighTicketId = ticket.Id,
                    Sequence = 1,
                    OriginalWeightGrams = WeightStorageMapper.ToGrams(draft.DraftWeight1)!.Value,
                    RecordedAt = draft.DraftWeight1RecordedAt!.Value,
                    PhotoPath = draft.IsWeight1LockedFromSavedTicket ? draft.DraftWeight1PhotoPath : null,
                    PhotoCaptureSucceeded = false,
                    PhotoErrorMessage = null
                }, cancellationToken);
            }

            if (draft.DraftWeight2.HasValue && !draft.IsWeight2LockedFromSavedTicket &&
                ticket.Events.All(e => e.Sequence != 2))
            {
                await _ticketRepository.AddEventAsync(new WeighEvent
                {
                    WeighTicketId = ticket.Id,
                    Sequence = 2,
                    OriginalWeightGrams = WeightStorageMapper.ToGrams(draft.DraftWeight2)!.Value,
                    RecordedAt = draft.DraftWeight2RecordedAt!.Value,
                    PhotoPath = draft.IsWeight2LockedFromSavedTicket ? draft.DraftWeight2PhotoPath : null,
                    PhotoCaptureSucceeded = false,
                    PhotoErrorMessage = null
                }, cancellationToken);
            }

            ticket = (await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken))!;
            ApplyCalculations(ticket, draft);
            ticket.UpdatedAt = DateTimeOffset.Now;
            await _ticketRepository.UpdateAsync(ticket, cancellationToken);

            return (await _ticketRepository.GetByIdWithEventsAsync(ticket.Id, cancellationToken))!;
        }, cancellationToken);
    }

    private void AddDraftEventsToTicket(WeighTicket ticket, WeighTicketDraft draft, string internalCode)
    {
        if (draft.DraftWeight1.HasValue)
        {
            ticket.Events.Add(CreateEventFromDraft(ticket.Id, draft, 1, internalCode));
        }

        if (draft.DraftWeight2.HasValue)
        {
            ticket.Events.Add(CreateEventFromDraft(ticket.Id, draft, 2, internalCode));
        }
    }

    private WeighEvent CreateEventFromDraft(int ticketId, WeighTicketDraft draft, int sequence, string internalCode)
    {
        var weight = draft.GetWeightKg(sequence)!.Value;
        var recordedAt = sequence == 1 ? draft.DraftWeight1RecordedAt!.Value : draft.DraftWeight2RecordedAt!.Value;

        return new WeighEvent
        {
            WeighTicketId = ticketId,
            Sequence = sequence,
            OriginalWeightGrams = WeightStorageMapper.ToGrams(weight)!.Value,
            RecordedAt = recordedAt,
            PhotoPath = IsLockedPhoto(draft, sequence)
                ? (sequence == 1 ? draft.DraftWeight1PhotoPath : draft.DraftWeight2PhotoPath)
                : null,
            PhotoCaptureSucceeded = false,
            PhotoErrorMessage = null
        };
    }

    private static bool IsLockedPhoto(WeighTicketDraft draft, int sequence) =>
        sequence == 1 ? draft.IsWeight1LockedFromSavedTicket : draft.IsWeight2LockedFromSavedTicket;

    private async Task ApplyDraftMetadataAsync(
        WeighTicket ticket,
        WeighTicketDraft draft,
        CancellationToken cancellationToken)
    {
        ticket.Notes = NullIfWhiteSpace(draft.DraftNotes);
        ticket.UnitPriceVndPerKg = WeightStorageMapper.ToVndPerKg(draft.DraftUnitPrice);

        if (NullIfWhiteSpace(draft.DraftCustomer) is { } customerName)
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

        if (NullIfWhiteSpace(draft.DraftCargoType) is { } cargoTypeName)
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

        if (NullIfWhiteSpace(draft.DraftVehicle) is { } plate)
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
        WeightStorageMapper.ApplyCalculationToTicket(
            ticket,
            draft.DraftWeight1,
            draft.DraftWeight2,
            draft.DraftUnitPrice);
    }

    private async Task<IReadOnlyList<string>> GetSimilarCustomerWarningsAsync(
        WeighTicketDraft draft,
        CancellationToken cancellationToken)
    {
        if (NullIfWhiteSpace(draft.DraftCustomer) is not { } name)
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

    private static WeighTicketListItem MapToListItem(WeighTicket ticket) =>
        new()
        {
            Id = ticket.Id,
            TicketDateTime = ticket.TicketDateTime,
            DisplayNumber = TicketNumberFormatter.ResolveDisplayNumber(
                ticket.SequenceNumber, ticket.TicketMonth, ticket.DisplayNumber),
            LicensePlate = ticket.LicensePlateSnapshot,
            CustomerName = ticket.CustomerNameSnapshot,
            CargoTypeName = ticket.CargoTypeNameSnapshot,
            GrossWeightKg = WeightStorageMapper.FromGrams(ticket.GrossWeightGrams),
            TareWeightKg = WeightStorageMapper.FromGrams(ticket.TareWeightGrams),
            NetWeightKg = WeightStorageMapper.FromGrams(ticket.NetWeightGrams),
            BillableWeightKg = WeightStorageMapper.FromGrams(ticket.BillableWeightGrams),
            UnitPriceVndPerKg = WeightStorageMapper.FromVndPerKg(ticket.UnitPriceVndPerKg),
            TotalAmountVnd = WeightStorageMapper.FromVnd(ticket.TotalAmountVnd),
            Notes = ticket.Notes,
            EventCount = ticket.Events.Count
        };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
