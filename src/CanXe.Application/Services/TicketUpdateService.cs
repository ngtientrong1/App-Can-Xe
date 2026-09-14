using CanXe.Application.Interfaces;
using CanXe.Application.Mapping;
using CanXe.Application.Models;
using CanXe.Domain.Entities;
using CanXe.Domain.Models;
using CanXe.Domain.Services;

namespace CanXe.Application.Services;

public sealed class TicketUpdateService
{
    private readonly IWeighTicketRepository _ticketRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICargoTypeRepository _cargoTypeRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public TicketUpdateService(
        IWeighTicketRepository ticketRepository,
        ICustomerRepository customerRepository,
        ICargoTypeRepository cargoTypeRepository,
        IVehicleRepository vehicleRepository)
    {
        _ticketRepository = ticketRepository;
        _customerRepository = customerRepository;
        _cargoTypeRepository = cargoTypeRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<WeighTicketDraft> LoadForEditAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy phiếu #{ticketId}.");

        var w1 = ticket.Events.FirstOrDefault(e => e.Sequence == 1);
        var w2 = ticket.Events.FirstOrDefault(e => e.Sequence == 2);
        var effective1 = w1 is null ? null : WeightStorageMapper.FromGrams(WeighEventWeightResolver.GetEffectiveWeightGrams(w1));
        var effective2 = w2 is null ? null : WeightStorageMapper.FromGrams(WeighEventWeightResolver.GetEffectiveWeightGrams(w2));

        return new WeighTicketDraft
        {
            IsEditMode = true,
            ExistingTicketId = ticket.Id,
            DisplayNumber = TicketNumberFormatter.ResolveDisplayNumber(
                ticket.SequenceNumber, ticket.TicketMonth, ticket.DisplayNumber),
            InternalCode = ticket.InternalCode,
            SequenceNumber = ticket.SequenceNumber,
            TicketDateTime = ticket.TicketDateTime,
            CreatedAt = ticket.CreatedAt,
            DraftCustomer = ticket.CustomerNameSnapshot,
            DraftCustomerId = ticket.CustomerId,
            DraftVehicle = ticket.LicensePlateSnapshot,
            DraftCargoType = ticket.CargoTypeNameSnapshot,
            DraftCargoTypeId = ticket.CargoTypeId,
            DraftUnitPrice = WeightStorageMapper.FromVndPerKg(ticket.UnitPriceVndPerKg),
            DraftNotes = ticket.Notes,
            DraftWeight1 = effective1,
            DraftWeight2 = effective2,
            LoadedEffectiveWeight1Kg = effective1,
            LoadedEffectiveWeight2Kg = effective2,
            DraftWeight1RecordedAt = w1?.RecordedAt,
            DraftWeight2RecordedAt = w2?.RecordedAt,
            DraftWeight1PhotoPath = w1?.PhotoPath,
            DraftWeight2PhotoPath = w2?.PhotoPath,
            SavedWeight1EventId = w1?.Id,
            SavedWeight2EventId = w2?.Id,
            IsWeight1LockedFromSavedTicket = w1 is not null,
            IsWeight2LockedFromSavedTicket = w2 is not null,
            LoadedWeight1HadOverride = w1 is not null && WeighEventWeightResolver.HasManualOverride(w1),
            LoadedWeight2HadOverride = w2 is not null && WeighEventWeightResolver.HasManualOverride(w2)
        };
    }

    public async Task<WeighTicketDraft> LoadForViewAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var draft = await LoadForEditAsync(ticketId, cancellationToken);
        draft.IsEditMode = false;
        return draft;
    }

    public async Task<UpdateTicketResult> UpdateAsync(
        WeighTicketDraft draft,
        string? editedBy,
        CancellationToken cancellationToken = default)
    {
        if (!draft.IsEditMode || draft.ExistingTicketId is not { } ticketId)
            return Fail("Không ở chế độ chỉnh sửa phiếu.");

        var ticket = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken);
        if (ticket is null)
            return Fail($"Không tìm thấy phiếu #{ticketId}.");

        var originalTicketDateTime = ticket.TicketDateTime;
        var originalDisplayNumber = ticket.DisplayNumber;
        var originalCreatedAt = ticket.CreatedAt;

        var w1 = ticket.Events.FirstOrDefault(e => e.Sequence == 1);
        var w2 = ticket.Events.FirstOrDefault(e => e.Sequence == 2);

        var auditLogs = new List<AuditLog>();
        var reasonText = WeightOverrideReasons.ResolveDisplayReason(
            draft.WeightOverrideReasonCode,
            draft.WeightOverrideReasonOther);

        var weightChanged = HasWeightChanged(draft, w1, w2);
        if (weightChanged)
        {
            if (!draft.DeveloperWeightUnlockEnabled)
                return Fail("Không thể sửa trọng lượng ngoài chế độ DEV.");

            var reasonError = TicketEditValidator.ValidateWeightOverrideReason(
                draft.WeightOverrideReasonCode,
                draft.WeightOverrideReasonOther);
            if (reasonError is not null)
                return Fail(reasonError);
        }

        auditLogs.AddIfNotNull(TicketEditAuditBuilder.BuildChange(
            ticketId, "CustomerName", ticket.CustomerNameSnapshot, draft.DraftCustomer, reasonText, editedBy, false));
        auditLogs.AddIfNotNull(TicketEditAuditBuilder.BuildChange(
            ticketId, "LicensePlate", ticket.LicensePlateSnapshot, draft.DraftVehicle, reasonText, editedBy, false));
        auditLogs.AddIfNotNull(TicketEditAuditBuilder.BuildChange(
            ticketId, "CargoTypeName", ticket.CargoTypeNameSnapshot, draft.DraftCargoType, reasonText, editedBy, false));
        auditLogs.AddIfNotNull(TicketEditAuditBuilder.BuildChange(
            ticketId, "UnitPrice",
            ticket.UnitPriceVndPerKg?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            draft.DraftUnitPrice?.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            reasonText, editedBy, false));
        auditLogs.AddIfNotNull(TicketEditAuditBuilder.BuildChange(
            ticketId, "Notes", ticket.Notes, draft.DraftNotes, reasonText, editedBy, false));

        await ApplyDraftMetadataAsync(ticket, draft, cancellationToken);

        var events = ticket.Events.ToList();
        ApplyWeightChanges(draft, events, w1, w2, ticketId, reasonText, editedBy, auditLogs);

        WeightStorageMapper.ApplyCalculationToTicket(ticket, draft.DraftWeight1, draft.DraftWeight2, draft.DraftUnitPrice);
        ticket.UpdatedAt = DateTimeOffset.Now;
        ticket.TicketDateTime = originalTicketDateTime;
        ticket.DisplayNumber = originalDisplayNumber;
        ticket.CreatedAt = originalCreatedAt;

        try
        {
            await _ticketRepository.UpdateTicketEditAsync(ticket, events, auditLogs, cancellationToken);
            var refreshed = await _ticketRepository.GetByIdWithEventsAsync(ticketId, cancellationToken);
            return new UpdateTicketResult
            {
                Success = true,
                UpdatedTicket = refreshed is null ? null : MapToListItem(refreshed)
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static bool HasWeightChanged(WeighTicketDraft draft, WeighEvent? w1, WeighEvent? w2)
    {
        var loaded1 = draft.LoadedEffectiveWeight1Kg;
        var loaded2 = draft.LoadedEffectiveWeight2Kg;
        return !NullableEquals(draft.DraftWeight1, loaded1) ||
               !NullableEquals(draft.DraftWeight2, loaded2) ||
               NullableHasValue(draft.DraftWeight1) != (w1 is not null) ||
               NullableHasValue(draft.DraftWeight2) != (w2 is not null);
    }

    private static void ApplyWeightChanges(
        WeighTicketDraft draft,
        List<WeighEvent> events,
        WeighEvent? w1,
        WeighEvent? w2,
        int ticketId,
        string? reasonText,
        string? editedBy,
        List<AuditLog> auditLogs)
    {
        SyncEvent(ref w1, events, ticketId, 1, draft.DraftWeight1, draft, reasonText, editedBy, auditLogs);
        SyncEvent(ref w2, events, ticketId, 2, draft.DraftWeight2, draft, reasonText, editedBy, auditLogs);
    }

    private static void SyncEvent(
        ref WeighEvent? existing,
        List<WeighEvent> events,
        int ticketId,
        int sequence,
        decimal? newWeightKg,
        WeighTicketDraft draft,
        string? reasonText,
        string? editedBy,
        List<AuditLog> auditLogs)
    {
        var field = sequence == 1 ? "Weight1" : "Weight2";
        var loaded = sequence == 1 ? draft.LoadedEffectiveWeight1Kg : draft.LoadedEffectiveWeight2Kg;

        if (!newWeightKg.HasValue)
        {
            if (existing is not null)
            {
                auditLogs.AddIfNotNull(TicketEditAuditBuilder.BuildChange(
                    ticketId,
                    field,
                    TicketEditAuditBuilder.FormatWeight(loaded),
                    null,
                    reasonText,
                    editedBy,
                    draft.DeveloperWeightUnlockEnabled));
                events.Remove(existing);
                existing = null;
            }

            return;
        }

        if (existing is null)
        {
            existing = new WeighEvent
            {
                WeighTicketId = ticketId,
                Sequence = sequence,
                OriginalWeightGrams = WeightStorageMapper.ToGrams(newWeightKg)!.Value,
                RecordedAt = DateTimeOffset.Now,
                IsManualOverride = draft.DeveloperWeightUnlockEnabled,
                OverrideWeightGrams = draft.DeveloperWeightUnlockEnabled
                    ? WeightStorageMapper.ToGrams(newWeightKg)
                    : null,
                OverrideReason = reasonText,
                OverrideAt = draft.DeveloperWeightUnlockEnabled ? DateTimeOffset.Now : null,
                OverrideBy = draft.DeveloperWeightUnlockEnabled ? editedBy : null,
                InputSource = draft.DeveloperWeightUnlockEnabled ? WeighInputSource.Manual : WeighInputSource.Hardware,
                CreatedByRole = draft.DeveloperWeightUnlockEnabled ? StationUserRole.Admin : StationUserRole.Operator,
                ManualReason = null
            };
            events.Add(existing);
            auditLogs.AddIfNotNull(TicketEditAuditBuilder.BuildChange(
                ticketId, field, null, TicketEditAuditBuilder.FormatWeight(newWeightKg), reasonText, editedBy, true));
            return;
        }

        var oldEffective = TicketEditAuditBuilder.FormatWeight(loaded);
        var newEffective = TicketEditAuditBuilder.FormatWeight(newWeightKg);
        if (!NullableEquals(newWeightKg, loaded))
        {
            existing.OverrideWeightGrams = WeightStorageMapper.ToGrams(newWeightKg);
            existing.IsManualOverride = true;
            existing.OverrideReason = reasonText;
            existing.OverrideAt = DateTimeOffset.Now;
            existing.OverrideBy = editedBy;
            existing.InputSource = WeighInputSource.Manual;
            existing.CreatedByRole = StationUserRole.Admin;
            if (string.Equals(draft.WeightOverrideReasonCode, WeightOverrideReasons.AdminInline, StringComparison.Ordinal))
                existing.ManualReason = null;
            auditLogs.AddIfNotNull(TicketEditAuditBuilder.BuildChange(
                ticketId, field, oldEffective, newEffective, reasonText, editedBy, true));
        }
    }

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

    public static bool TicketMatchesFilter(WeighTicket ticket, WeighTicketFilter filter) =>
        MatchesFilterInternal(ticket, filter);

    public static bool MatchesFilterInternal(WeighTicket ticket, WeighTicketFilter filter)
    {
        if (filter.FromDate is { } from && ticket.TicketDateTime < from)
            return false;
        if (filter.ToDate is { } to && ticket.TicketDateTime > to)
            return false;
        if (!string.IsNullOrWhiteSpace(filter.CustomerName) &&
            (ticket.CustomerNameSnapshot is null ||
             !ticket.CustomerNameSnapshot.Contains(filter.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!string.IsNullOrWhiteSpace(filter.CargoTypeName) &&
            (ticket.CargoTypeNameSnapshot is null ||
             !ticket.CargoTypeNameSnapshot.Contains(filter.CargoTypeName.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!string.IsNullOrWhiteSpace(filter.LicensePlate) &&
            (ticket.LicensePlateSnapshot is null ||
             !PlateNormalizer.Normalize(ticket.LicensePlateSnapshot).Contains(PlateNormalizer.Normalize(filter.LicensePlate), StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!string.IsNullOrWhiteSpace(filter.DisplayNumber) &&
            !ticket.DisplayNumber.Contains(filter.DisplayNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if (filter.UnitPriceVndPerKg is { } price)
        {
            var vnd = (int)Math.Round(price, 0, MidpointRounding.AwayFromZero);
            if (ticket.UnitPriceVndPerKg != vnd)
                return false;
        }

        if (filter.UnitPriceFromVndPerKg is { } fromPrice)
        {
            var vndFrom = (int)Math.Round(fromPrice, 0, MidpointRounding.AwayFromZero);
            if (ticket.UnitPriceVndPerKg < vndFrom)
                return false;
        }

        if (filter.UnitPriceToVndPerKg is { } toPrice)
        {
            var vndTo = (int)Math.Round(toPrice, 0, MidpointRounding.AwayFromZero);
            if (ticket.UnitPriceVndPerKg > vndTo)
                return false;
        }

        return true;
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

    private static string? FormatPrice(int? vndPerKg) =>
        vndPerKg?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? FormatPrice(decimal? price) =>
        price?.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    private static UpdateTicketResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool NullableEquals(decimal? a, decimal? b) =>
        a.HasValue == b.HasValue && (!a.HasValue || a.Value == b.Value);

    private static bool NullableHasValue(decimal? value) => value.HasValue;
}

internal static class AuditLogListExtensions
{
    public static void AddIfNotNull(this List<AuditLog> list, AuditLog? item)
    {
        if (item is not null)
            list.Add(item);
    }
}
