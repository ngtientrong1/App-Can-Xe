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
    private readonly IPhotoStorageService _photoStorage;
    private readonly IUserNotificationService? _notificationService;
    private readonly TicketUpdateService _ticketUpdateService;
    private readonly List<Task> _pendingPhotoTasks = [];

    public Task WaitForPendingPhotosAsync() => Task.WhenAll(_photoTasksSnapshot());

    private Task[] _photoTasksSnapshot()
    {
        lock (_pendingPhotoTasks)
            return _pendingPhotoTasks.ToArray();
    }

    public WeighTicketService(
        IWeighTicketRepository ticketRepository,
        ICustomerRepository customerRepository,
        ICargoTypeRepository cargoTypeRepository,
        IVehicleRepository vehicleRepository,
        IScaleService scaleService,
        ICameraService cameraService,
        IPhotoStorageService photoStorage,
        TicketUpdateService ticketUpdateService,
        IUserNotificationService? notificationService = null)
    {
        _ticketRepository = ticketRepository;
        _customerRepository = customerRepository;
        _cargoTypeRepository = cargoTypeRepository;
        _vehicleRepository = vehicleRepository;
        _scaleService = scaleService;
        _cameraService = cameraService;
        _photoStorage = photoStorage;
        _ticketUpdateService = ticketUpdateService;
        _notificationService = notificationService;
    }

    public Task<WeighTicketDraft> LoadTicketForEditAsync(
        int ticketId,
        CancellationToken cancellationToken = default) =>
        _ticketUpdateService.LoadForEditAsync(ticketId, cancellationToken);

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

    public async Task<CaptureWeightResult> CaptureWeightAsync(
        WeighTicketDraft draft,
        int sequence,
        CancellationToken cancellationToken = default)
    {
        if (sequence is not (1 or 2))
            return new CaptureWeightResult { Success = false, ErrorMessage = "Lần cân không hợp lệ." };

        if (sequence == 1 && !DraftWorkflowRules.CanUpdateWeight1(
                draft.IsWeight1LockedFromSavedTicket,
                draft.DraftWeight2.HasValue,
                draft.DeveloperWeight1OverrideEnabled))
            return new CaptureWeightResult { Success = false, ErrorMessage = "Cân lần 1 đã khóa sau khi có cân lần 2." };

        if (sequence == 1 && draft.IsWeight1LockedFromSavedTicket)
            return new CaptureWeightResult { Success = false, ErrorMessage = "Không thể sửa cân lần 1 đã lưu." };

        if (sequence == 2 && draft.IsWeight2LockedFromSavedTicket)
            return new CaptureWeightResult { Success = false, ErrorMessage = "Không thể sửa cân lần 2 đã lưu." };

        var isUpdate = sequence == 1 ? draft.DraftWeight1.HasValue : draft.DraftWeight2.HasValue;
        var previousPhotoPath = sequence == 1 ? draft.DraftWeight1PhotoPath : draft.DraftWeight2PhotoPath;

        if (isUpdate && !string.IsNullOrEmpty(previousPhotoPath) && !IsLockedPhoto(draft, sequence))
            _photoStorage.DeletePhotoIfExists(previousPhotoPath);

        var weightKg = Math.Round(await _scaleService.GetCurrentWeightAsync(cancellationToken), 0, MidpointRounding.AwayFromZero);
        var recordedAt = DateTimeOffset.Now;

        draft.SetWeightDraft(sequence, weightKg, recordedAt);
        draft.SetPhotoPending(sequence);

        if (sequence == 1)
        {
            draft.DraftWeight1PhotoPath = null;
            draft.DraftWeight1PhotoError = null;
        }
        else
        {
            draft.DraftWeight2PhotoPath = null;
            draft.DraftWeight2PhotoError = null;
        }

        if (!IsLockedPhoto(draft, sequence))
        {
            var photoTask = CapturePhotoInBackgroundAsync(draft, sequence, previousPhotoPath);
            lock (_pendingPhotoTasks)
                _pendingPhotoTasks.Add(photoTask);
        }

        return new CaptureWeightResult
        {
            Success = true,
            Sequence = sequence,
            WeightKg = weightKg,
            IsUpdate = isUpdate
        };
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

            _photoStorage.CleanupDraftSession(draft.DraftSessionId.ToString("N"));

            return new SaveTicketResult
            {
                Success = true,
                SavedTicket = MapToListItem(ticket),
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
        if (!draft.IsWeight1LockedFromSavedTicket)
            _photoStorage.DeletePhotoIfExists(draft.DraftWeight1PhotoPath);

        if (!draft.IsWeight2LockedFromSavedTicket)
            _photoStorage.DeletePhotoIfExists(draft.DraftWeight2PhotoPath);

        _photoStorage.CleanupDraftSession(draft.DraftSessionId.ToString("N"));
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
            Weight1PhotoAvailable = IsPhotoAvailable(w1?.PhotoPath, w1?.PhotoCaptureSucceeded),
            Weight1PhotoStatusText = GetPhotoStatusText(w1?.PhotoPath, w1?.PhotoCaptureSucceeded),
            Weight2Kg = weight2,
            Weight2RecordedAt = w2?.RecordedAt,
            Weight2PhotoPath = w2?.PhotoPath,
            Weight2PhotoAvailable = IsPhotoAvailable(w2?.PhotoPath, w2?.PhotoCaptureSucceeded),
            Weight2PhotoStatusText = GetPhotoStatusText(w2?.PhotoPath, w2?.PhotoCaptureSucceeded),
            GrossWeightKg = WeightStorageMapper.FromGrams(ticket.GrossWeightGrams),
            TareWeightKg = WeightStorageMapper.FromGrams(ticket.TareWeightGrams),
            NetWeightKg = WeightStorageMapper.FromGrams(ticket.NetWeightGrams),
            DeductionWeightKg = WeightStorageMapper.FromGrams(ticket.DeductionWeightGrams),
            BillableWeightKg = WeightStorageMapper.FromGrams(ticket.BillableWeightGrams),
            TotalAmountVnd = WeightStorageMapper.FromVnd(ticket.TotalAmountVnd),
            IsSingleWeigh = WeightCalculator.IsSingleWeigh(weight1, weight2)
        };
    }

    private static bool IsPhotoAvailable(string? path, bool? captureSucceeded) =>
        captureSucceeded == true && !string.IsNullOrEmpty(path) && File.Exists(path);

    private static string GetPhotoStatusText(string? path, bool? captureSucceeded)
    {
        if (captureSucceeded != true || string.IsNullOrEmpty(path))
            return "Không có ảnh";

        return File.Exists(path) ? "Có ảnh" : "Ảnh đã hết thời hạn lưu";
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
                    PhotoPath = ResolveOfficialPhotoPath(draft, 1, ticket.InternalCode),
                    PhotoCaptureSucceeded = draft.DraftWeight1PhotoStatus == DraftPhotoStatus.Valid,
                    PhotoErrorMessage = draft.DraftWeight1PhotoError
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
                    PhotoPath = ResolveOfficialPhotoPath(draft, 2, ticket.InternalCode),
                    PhotoCaptureSucceeded = draft.DraftWeight2PhotoStatus == DraftPhotoStatus.Valid,
                    PhotoErrorMessage = draft.DraftWeight2PhotoError
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
        var photoStatus = sequence == 1 ? draft.DraftWeight1PhotoStatus : draft.DraftWeight2PhotoStatus;
        var photoError = sequence == 1 ? draft.DraftWeight1PhotoError : draft.DraftWeight2PhotoError;

        return new WeighEvent
        {
            WeighTicketId = ticketId,
            Sequence = sequence,
            OriginalWeightGrams = WeightStorageMapper.ToGrams(weight)!.Value,
            RecordedAt = recordedAt,
            PhotoPath = ResolveOfficialPhotoPath(draft, sequence, internalCode),
            PhotoCaptureSucceeded = photoStatus == DraftPhotoStatus.Valid,
            PhotoErrorMessage = photoError
        };
    }

    private string? ResolveOfficialPhotoPath(WeighTicketDraft draft, int sequence, string internalCode)
    {
        var status = sequence == 1 ? draft.DraftWeight1PhotoStatus : draft.DraftWeight2PhotoStatus;
        if (status != DraftPhotoStatus.Valid)
            return null;

        var draftPath = sequence == 1 ? draft.DraftWeight1PhotoPath : draft.DraftWeight2PhotoPath;
        var recordedAt = sequence == 1 ? draft.DraftWeight1RecordedAt : draft.DraftWeight2RecordedAt;

        if (IsLockedPhoto(draft, sequence))
            return draftPath;

        if (string.IsNullOrEmpty(draftPath) || recordedAt is null)
            return null;

        var officialPath = _photoStorage.GetOfficialPhotoPath(internalCode, sequence, recordedAt.Value);
        if (File.Exists(draftPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(officialPath)!);
            File.Copy(draftPath, officialPath, overwrite: true);
            _photoStorage.DeletePhotoIfExists(draftPath);
        }

        return officialPath;
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

    private async Task CapturePhotoInBackgroundAsync(
        WeighTicketDraft draft,
        int sequence,
        string? previousPhotoPath)
    {
        try
        {
            var sessionId = draft.DraftSessionId.ToString("N");
            var targetPath = _photoStorage.GetDraftPhotoPath(sessionId, sequence);
            var request = new PhotoCaptureRequest
            {
                StorageKind = PhotoStorageKind.Draft,
                ReferenceCode = sessionId,
                WeighSequence = sequence
            };

            var result = await _cameraService.CaptureAsync(request, targetPath);

            if (result.Success)
            {
                if (sequence == 1)
                {
                    draft.DraftWeight1PhotoPath = result.FilePath;
                    draft.DraftWeight1PhotoStatus = DraftPhotoStatus.Valid;
                    draft.DraftWeight1PhotoError = null;
                }
                else
                {
                    draft.DraftWeight2PhotoPath = result.FilePath;
                    draft.DraftWeight2PhotoStatus = DraftPhotoStatus.Valid;
                    draft.DraftWeight2PhotoError = null;
                }

                if (!string.IsNullOrEmpty(previousPhotoPath) &&
                    !string.Equals(previousPhotoPath, result.FilePath, StringComparison.OrdinalIgnoreCase))
                    _photoStorage.DeletePhotoIfExists(previousPhotoPath);
            }
            else
            {
                draft.InvalidatePhotoDraft(sequence);
                if (sequence == 1)
                    draft.DraftWeight1PhotoError = result.ErrorMessage;
                else
                    draft.DraftWeight2PhotoError = result.ErrorMessage;

                _photoStorage.DeletePhotoIfExists(previousPhotoPath);
                _notificationService?.Notify($"Camera lỗi: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            draft.InvalidatePhotoDraft(sequence);
            if (sequence == 1)
                draft.DraftWeight1PhotoError = ex.Message;
            else
                draft.DraftWeight2PhotoError = ex.Message;

            _photoStorage.DeletePhotoIfExists(previousPhotoPath);
            _notificationService?.Notify($"Camera lỗi: {ex.Message}");
        }
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
