using System.Globalization;
using System.Windows;
using CanXe.Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private readonly record struct TicketFormSnapshot(
        string? Customer,
        string? Plate,
        string? Cargo,
        string? Notes,
        string? UnitPrice,
        decimal? Weight1,
        decimal? Weight2,
        string? DevWeight1,
        string? DevWeight2);

    [ObservableProperty] private TicketFormMode _formMode = TicketFormMode.Creating;
    [ObservableProperty] private int? _activeTicketId;
    [ObservableProperty] private string? _viewingTicketNumber;

    private TicketFormSnapshot? _formSnapshot;
    private int _selectionRequestVersion;
    private CancellationTokenSource? _selectionLoadCts;
    private bool _suppressSelectionChange;
    private WeighTicketListItem? _selectionAnchorItem;
    private int _deleteInProgressTicketId;

    public bool IsViewModeActionsVisible => FormMode == TicketFormMode.Viewing;

    public string ViewBannerTitle =>
        FormMode == TicketFormMode.Viewing && !string.IsNullOrWhiteSpace(ViewingTicketNumber)
            ? $"ĐANG XEM PHIẾU {ViewingTicketNumber}"
            : FormMode == TicketFormMode.AwaitingSecondWeigh && !string.IsNullOrWhiteSpace(ViewingTicketNumber)
                ? $"CHỜ CÂN LẦN 2 — PHIẾU {ViewingTicketNumber}"
                : string.Empty;

    partial void OnActiveTicketIdChanged(int? value)
    {
        PrintTicketCommand.NotifyCanExecuteChanged();
        ViewTicketCommand.NotifyCanExecuteChanged();
    }

    partial void OnFormModeChanged(TicketFormMode value)
    {
        IsContinuationMode = value == TicketFormMode.AwaitingSecondWeigh;
        OnPropertyChanged(nameof(IsCreateModeActionsVisible));
        OnPropertyChanged(nameof(IsEditModeActionsVisible));
        OnPropertyChanged(nameof(IsViewModeActionsVisible));
        OnPropertyChanged(nameof(ViewBannerTitle));
        UpdateButtonStates();
        UpdateButtonLabels();
        PrintTicketCommand.NotifyCanExecuteChanged();
        ViewTicketCommand.NotifyCanExecuteChanged();
    }

    partial void OnViewingTicketNumberChanged(string? value) =>
        OnPropertyChanged(nameof(ViewBannerTitle));

    partial void OnSelectedTicketChanged(WeighTicketListItem? value)
    {
        if (_suppressSelectionChange)
            return;

        PrintTicketCommand.NotifyCanExecuteChanged();
        ViewTicketCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public async Task OpenTicketFromListAsync(WeighTicketListItem? item)
    {
        if (item is null)
            return;

        if (IsFormDirty())
        {
            var decision = PromptUnsavedChangesForOpen();
            if (decision == UnsavedChangeDecision.Stay)
                return;

            if (decision == UnsavedChangeDecision.SaveAndContinue)
            {
                if (!await TrySaveCurrentFormAsync())
                    return;
            }
        }

        await LoadSelectedTicketAsync(item);
    }

    private async Task LoadSelectedTicketAsync(WeighTicketListItem item)
    {
        var requestVersion = ++_selectionRequestVersion;
        _selectionLoadCts?.Cancel();
        _selectionLoadCts?.Dispose();
        _selectionLoadCts = new CancellationTokenSource();
        var ct = _selectionLoadCts.Token;

        try
        {
            WeighTicketDraft draft;
            TicketFormMode mode;
            if (item.EventCount is > 0 and < 2)
            {
                draft = await _weighTicketService.LoadTicketForContinuationAsync(item.Id, ct);
                mode = TicketFormMode.AwaitingSecondWeigh;
            }
            else
            {
                draft = await _weighTicketService.LoadTicketForViewAsync(item.Id, ct);
                mode = TicketFormMode.Viewing;
            }

            if (ct.IsCancellationRequested || requestVersion != _selectionRequestVersion)
                return;

            _weighTicketService.CancelDraft(_draft);
            _draft = draft;
            FormMode = mode;
            ActiveTicketId = item.Id;
            ViewingTicketNumber = item.DisplayNumber;
            IsEditingExistingTicket = false;
            EditingTicketId = null;
            EditingTicketNumber = null;
            IsDevWeightEditUnlocked = false;
            DevWeight1Text = _draft.DraftWeight1?.ToString("N0", CultureInfo.CurrentCulture);
            DevWeight2Text = _draft.DraftWeight2?.ToString("N0", CultureInfo.CurrentCulture);
            WeightOverrideReasonCode = null;
            WeightOverrideReasonOther = null;
            LoadBindingsFromDraft();
            CaptureFormSnapshot();
            UpdateWeightOverrideUi();
            UpdateButtonStates();
            UpdateButtonLabels();

            StatusMessage = mode == TicketFormMode.AwaitingSecondWeigh
                ? $"Đang tiếp tục phiếu {item.DisplayNumber} (CHỜ CÂN LẦN 2). Bấm LẤY CÂN LẦN 2."
                : $"Đang xem phiếu {item.DisplayNumber}.";
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer open request
        }
        catch (Exception ex)
        {
            if (requestVersion == _selectionRequestVersion)
                StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UnlockViewForEditAsync()
    {
        if (FormMode != TicketFormMode.Viewing || ActiveTicketId is not int ticketId)
            return;

        var item = Tickets.FirstOrDefault(t => t.Id == ticketId) ?? SelectedTicket;
        if (item is null)
            return;

        await BeginEditTicketAsync(item);
    }

    partial void OnSelectedTicketChanging(WeighTicketListItem? value) =>
        _selectionAnchorItem = SelectedTicket;

    private bool IsFormDirty()
    {
        if (FormMode is TicketFormMode.Browsing or TicketFormMode.Creating)
            return HasUnsavedDraftContent();

        if (FormMode == TicketFormMode.Editing)
            return IsDevWeightEditUnlocked && (HasDevWeightChanged() || HasMetadataChangedFromSnapshot());

        if (_formSnapshot is null)
            return HasUnsavedDraftContent();

        return HasMetadataChangedFromSnapshot() || HasDraftWeightChangedFromSnapshot();
    }

    private bool HasUnsavedDraftContent()
    {
        SyncDraftFromBindings();
        return _draft.HasAnyWeight
            || !string.IsNullOrWhiteSpace(_draft.DraftCustomer)
            || !string.IsNullOrWhiteSpace(_draft.DraftVehicle)
            || !string.IsNullOrWhiteSpace(_draft.DraftCargoType)
            || !string.IsNullOrWhiteSpace(_draft.DraftNotes)
            || _draft.DraftUnitPrice.HasValue;
    }

    private bool HasMetadataChangedFromSnapshot()
    {
        if (_formSnapshot is null)
            return false;

        SyncDraftFromBindings();
        return !string.Equals(_formSnapshot.Value.Customer, CustomerName, StringComparison.Ordinal)
            || !string.Equals(_formSnapshot.Value.Plate, LicensePlate, StringComparison.Ordinal)
            || !string.Equals(_formSnapshot.Value.Cargo, CargoTypeName, StringComparison.Ordinal)
            || !string.Equals(_formSnapshot.Value.Notes, Notes, StringComparison.Ordinal)
            || !string.Equals(_formSnapshot.Value.UnitPrice, UnitPriceText, StringComparison.Ordinal);
    }

    private bool HasDraftWeightChangedFromSnapshot()
    {
        if (_formSnapshot is null)
            return false;

        SyncDraftFromBindings();
        return _draft.DraftWeight1 != _formSnapshot.Value.Weight1
            || _draft.DraftWeight2 != _formSnapshot.Value.Weight2;
    }

    private void CaptureFormSnapshot()
    {
        SyncDraftFromBindings();
        _formSnapshot = new TicketFormSnapshot(
            CustomerName,
            LicensePlate,
            CargoTypeName,
            Notes,
            UnitPriceText,
            _draft.DraftWeight1,
            _draft.DraftWeight2,
            DevWeight1Text,
            DevWeight2Text);
    }

    private void ClearFormSnapshot() => _formSnapshot = null;

    private enum UnsavedChangeDecision
    {
        Stay,
        DiscardAndContinue,
        SaveAndContinue
    }

    private static UnsavedChangeDecision PromptUnsavedChangesForOpen()
    {
        var result = MessageBox.Show(
            "Phiếu hiện tại có thay đổi chưa lưu.\n\nChọn Lưu để lưu và mở phiếu, Không để bỏ thay đổi, Hủy để ở lại.",
            "CanXe",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => UnsavedChangeDecision.SaveAndContinue,
            MessageBoxResult.No => UnsavedChangeDecision.DiscardAndContinue,
            _ => UnsavedChangeDecision.Stay
        };
    }

    private async Task<bool> TrySaveCurrentFormAsync()
    {
        if (FormMode == TicketFormMode.Editing)
        {
            await UpdateTicketAsync();
            return !IsEditingExistingTicket;
        }

        var result = await _weighTicketService.SaveAsync(_draft, BuildCurrentFilter());
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Lưu thất bại.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task ExitViewAsync()
    {
        _weighTicketService.CancelDraft(_draft);
        await ResetDraftAsync();
        StatusMessage = "Đã đóng phiếu đang xem.";
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private async Task DeleteTicketAsync(WeighTicketListItem? item)
    {
        if (item is null || !IsDeveloperDeleteVisible)
            return;

        if (_deleteInProgressTicketId == item.Id)
            return;

        if (!_developerAuthorization.CanDeleteTickets)
        {
            MessageBox.Show(
                "Bạn không có quyền xóa phiếu cân.",
                "CanXe",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var awaitingSecondWeigh = item.EventCount is > 0 and < 2;
        var message = awaitingSecondWeigh
            ? $"Bạn có chắc muốn xóa phiếu {item.DisplayNumber}?\n\nPhiếu này đang chờ cân lần 2.\nXóa phiếu sẽ hủy quy trình cân đang dở.\n\nPhiếu sẽ bị ẩn khỏi danh sách vận hành.\nThao tác này chỉ dành cho Nhà phát triển."
            : $"Bạn có chắc muốn xóa phiếu {item.DisplayNumber}?\n\nPhiếu sẽ bị ẩn khỏi danh sách vận hành.\nThao tác này chỉ dành cho Nhà phát triển.";

        var confirm = MessageBox.Show(
            message,
            "XÁC NHẬN XÓA PHIẾU",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);

        if (confirm != MessageBoxResult.OK)
            return;

        _deleteInProgressTicketId = item.Id;
        try
        {
            var result = await _ticketDeleteService.SoftDeleteAsync(item.Id).ConfigureAwait(true);
            if (!result.Success)
            {
                if (!string.Equals(result.ErrorMessage, "cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? "Không thể xóa phiếu cân.",
                        "CanXe",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                return;
            }

            Tickets.Remove(item);
            if (SelectedTicket?.Id == item.Id)
                SelectedTicket = null;

            if (ActiveTicketId == item.Id)
            {
                _weighTicketService.CancelDraft(_draft);
                await ResetDraftAsync();
            }

            await RefreshFooterSummaryAsync();
            _ = _printNotificationService.ShowDeleteSuccessToastAsync(result.DisplayNumber);
            StatusMessage = $"Đã xóa phiếu {result.DisplayNumber}.";
        }
        finally
        {
            _deleteInProgressTicketId = 0;
        }
    }
}
