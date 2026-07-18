using System.Globalization;
using System.Windows;
using System.Windows.Input;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Views.Admin;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private IAdminAuthorizationService _adminAuthorization = null!;
    private IUserPermissionService _permissions = null!;

    [ObservableProperty] private bool _isAdminUnlocked;
    [ObservableProperty] private bool _isAdminBadgeVisible;
    [ObservableProperty] private bool _isManualWeighButtonVisible;
    [ObservableProperty] private bool _isAdminUnlockButtonVisible = true;
    [ObservableProperty] private bool _isAdminLockButtonVisible;
    [ObservableProperty] private string? _weight1SourceDisplay;
    [ObservableProperty] private string? _weight2SourceDisplay;
    [ObservableProperty] private bool _isInlineWeightEditEnabled;
    [ObservableProperty] private string? _adminInlineWeightHint;
    [ObservableProperty] private bool _isWeight1InlineEditing;
    [ObservableProperty] private bool _isWeight2InlineEditing;
    [ObservableProperty] private string? _weight1InlineEditText;
    [ObservableProperty] private string? _weight2InlineEditText;

    private void WireAdminServices(
        IAdminAuthorizationService adminAuthorization,
        IUserPermissionService permissions)
    {
        _adminAuthorization = adminAuthorization;
        _permissions = permissions;
        _adminAuthorization.AdminSessionChanged += (_, _) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                RefreshAdminUiState();
                return;
            }

            if (dispatcher.CheckAccess())
                RefreshAdminUiState();
            else
                dispatcher.Invoke(RefreshAdminUiState);
        };
        RefreshAdminUiState();
    }

    private void RefreshAdminUiState()
    {
        IsAdminUnlocked = _adminAuthorization.IsAdminUnlocked;
        IsAdminBadgeVisible = IsAdminUnlocked;
        IsAdminLockButtonVisible = IsAdminUnlocked;
        IsAdminUnlockButtonVisible = !IsAdminUnlocked;
        // Phase 6 rc2: inline edit on weight cards; no "NHẬP CÂN TAY" button on main UI.
        IsManualWeighButtonVisible = false;
        IsInlineWeightEditEnabled = IsAdminUnlocked &&
            FormMode is TicketFormMode.Creating
                or TicketFormMode.AwaitingSecondWeigh
                or TicketFormMode.Viewing
                or TicketFormMode.Editing;
        AdminInlineWeightHint = IsInlineWeightEditEnabled
            ? "Nhấp số cân để sửa ✎"
            : null;
        IsDeveloperDeleteVisible = _permissions.HasPermission(AdminPermission.CanDeleteTicket);
        IsDeveloperWeightUnlockVisible = IsAdminUnlocked &&
            (FormMode == TicketFormMode.Editing || IsEditingExistingTicket);
        if (!IsInlineWeightEditEnabled)
        {
            IsWeight1InlineEditing = false;
            IsWeight2InlineEditing = false;
        }

        Catalog.NotifyAdminPermissionsChanged();
        Settings.RefreshBackupAdminState();
        UpdateWeightSourceDisplays();
    }

    private void UpdateWeightSourceDisplays()
    {
        Weight1SourceDisplay = FormatWeightSource(_draft.DraftWeight1InputSource, _draft.HasCapturedWeight1);
        Weight2SourceDisplay = FormatWeightSource(_draft.DraftWeight2InputSource, _draft.HasCapturedWeight2);
    }

    private static string? FormatWeightSource(WeighInputSource source, bool hasValue)
    {
        if (!hasValue)
            return null;
        return source == WeighInputSource.Manual
            ? "Nguồn cân: Cân tay"
            : "Nguồn cân: Cân điện tử";
    }

    [RelayCommand]
    private void UnlockAdmin()
    {
        var dialog = new AdminUnlockWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dialog.ShowDialog() != true)
            return;

        var result = _adminAuthorization.TryUnlock(dialog.Password);
        if (!result.Success)
        {
            MessageBox.Show(
                result.ErrorMessage ?? "Mã Admin không đúng.",
                "Mở khóa Admin",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        RefreshAdminUiState();
        StatusMessage = "Đã mở khóa Admin.";
    }

    [RelayCommand]
    private void LockAdmin()
    {
        _adminAuthorization.Lock("manual");
        RefreshAdminUiState();
        StatusMessage = "Đã khóa Admin.";
    }

    [RelayCommand]
    private void BeginInlineWeightEdit(int sequence)
    {
        if (!IsInlineWeightEditEnabled)
            return;

        // Draft/new ticket: CanManualWeigh. Completed viewing/editing: CanEditCompletedTicket.
        var needsCompletedPermission = FormMode is TicketFormMode.Viewing or TicketFormMode.Editing;
        var permission = needsCompletedPermission
            ? AdminPermission.CanEditCompletedTicket
            : AdminPermission.CanManualWeigh;
        if (!EnsureAdminOrNotify(permission, "ADMIN_WEIGHT_EDIT"))
            return;
        if (sequence == 1)
        {
            IsWeight2InlineEditing = false;
            Weight1InlineEditText = _draft.DraftWeight1?.ToString("0", CultureInfo.InvariantCulture) ?? string.Empty;
            IsWeight1InlineEditing = true;
        }
        else
        {
            IsWeight1InlineEditing = false;
            Weight2InlineEditText = _draft.DraftWeight2?.ToString("0", CultureInfo.InvariantCulture) ?? string.Empty;
            IsWeight2InlineEditing = true;
        }
    }

    [RelayCommand]
    private async Task ApplyInlineWeightEditAsync(int sequence)
    {
        if (sequence is not (1 or 2))
            return;

        var text = sequence == 1 ? Weight1InlineEditText : Weight2InlineEditText;
        if (!ManualSimulationInputHelper.TryParseKg(text, out var kg, out _))
        {
            StatusMessage = "Số cân không hợp lệ.";
            _ = ShowToastAsync("Số cân không hợp lệ.");
            CancelInlineWeightEdit(sequence);
            return;
        }

        var ok = await ApplyAdminInlineWeightKgAsync(sequence, kg);
        if (!ok)
            CancelInlineWeightEdit(sequence);
        else if (sequence == 1)
            IsWeight1InlineEditing = false;
        else
            IsWeight2InlineEditing = false;
    }

    [RelayCommand]
    private void CancelInlineWeightEdit(int sequence)
    {
        if (sequence == 1)
            IsWeight1InlineEditing = false;
        else
            IsWeight2InlineEditing = false;
    }

    /// <summary>Public entry for tests / command routing.</summary>
    public async Task<bool> ApplyAdminInlineWeightKgAsync(int sequence, decimal kg)
    {
        var needsCompletedPermission =
            FormMode is TicketFormMode.Viewing or TicketFormMode.Editing ||
            (sequence == 1 && _draft.IsWeight1LockedFromSavedTicket) ||
            (sequence == 2 && _draft.IsWeight2LockedFromSavedTicket);

        var permission = needsCompletedPermission
            ? AdminPermission.CanEditCompletedTicket
            : AdminPermission.CanManualWeigh;

        if (!EnsureAdminOrNotify(permission, "ADMIN_WEIGHT_EDIT"))
            return false;

        if (kg < 0)
        {
            StatusMessage = "Số cân không hợp lệ.";
            return false;
        }

        SyncDraftFromBindings();
        var oldWeight = sequence == 1 ? _draft.DraftWeight1 : _draft.DraftWeight2;
        var oldSource = sequence == 1 ? _draft.DraftWeight1InputSource : _draft.DraftWeight2InputSource;

        var bypassLock = _draft.ExistingTicketId.HasValue &&
            ((sequence == 1 && _draft.IsWeight1LockedFromSavedTicket) ||
             (sequence == 2 && _draft.IsWeight2LockedFromSavedTicket) ||
             FormMode is TicketFormMode.Viewing or TicketFormMode.Editing);

        if (FormMode == TicketFormMode.Viewing)
            TransitionViewingToAdminEditingPreserveDraft();

        var result = await _weighTicketService.ApplyAdminInlineWeightAsync(
            _draft,
            sequence,
            kg,
            StationUserRole.Admin,
            bypassSavedLock: bypassLock);

        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Số cân không hợp lệ.";
            _ = ShowToastAsync("Số cân không hợp lệ.");
            return false;
        }

        if (bypassLock || FormMode == TicketFormMode.Editing)
        {
            IsDevWeightEditUnlocked = true;
            _draft.DeveloperWeightUnlockEnabled = true;
            WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
            WeightOverrideReasonOther = null;
        }

        // Keep DEV text fields in sync with draft so SyncDraftFromBindings on CẬP NHẬT
        // does not overwrite Admin inline edits with stale values from ticket load.
        DevWeight1Text = _draft.DraftWeight1?.ToString("N0", CultureInfo.CurrentCulture);
        DevWeight2Text = _draft.DraftWeight2?.ToString("N0", CultureInfo.CurrentCulture);

        _adminAuthorization.TouchAdminActivity();
        AdminAuditLogger.Write(
            "ADMIN_WEIGHT_EDIT",
            "SUCCESS",
            "Admin",
            ticketId: ActiveTicketId?.ToString() ?? "draft",
            note: $"weighNo={sequence}; oldWeight={oldWeight}; newWeight={kg}; oldSource={oldSource}; newSource=Manual");

        LoadBindingsFromDraft();
        UpdateWeightSourceDisplays();
        UpdateWeightOverrideUi();
        UpdateButtonStates();
        UpdateButtonLabels();
        OnPropertyChanged(nameof(IsTicketDirty));
        StatusMessage = $"Đã sửa cân lần {sequence}: {kg:N0} kg.";
        return true;
    }

    private void TransitionViewingToAdminEditingPreserveDraft()
    {
        if (FormMode != TicketFormMode.Viewing || ActiveTicketId is not int ticketId)
            return;

        FormMode = TicketFormMode.Editing;
        IsEditingExistingTicket = true;
        EditingTicketId = ticketId;
        EditingTicketNumber = ViewingTicketNumber;
        _draft.IsEditMode = true;
        _draft.ExistingTicketId = ticketId;
        IsDevWeightEditUnlocked = true;
        _draft.DeveloperWeightUnlockEnabled = true;
        WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        RefreshAdminUiState();
    }

    [RelayCommand]
    private async Task OpenManualWeighAsync()
    {
        var gate = _permissions.EnsurePermission(AdminPermission.CanManualWeigh, "MANUAL_WEIGH");
        if (!gate.Allowed)
        {
            MessageBox.Show(gate.ErrorMessage!, "CanXe", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshAdminUiState();
            return;
        }

        var prefer1 = !_draft.HasCapturedWeight1;
        var prefer2 = _draft.HasCapturedWeight1 && !_draft.HasCapturedWeight2;
        var dialog = new ManualWeighWindow(prefer1, prefer2)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dialog.ShowDialog() != true || dialog.SelectedSequence is not int sequence)
            return;

        if (!ManualSimulationInputHelper.TryParseKg(dialog.WeightText, out var kg, out var parseError))
        {
            MessageBox.Show(parseError ?? "Trọng lượng không hợp lệ.", "Nhập cân tay", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var reason = dialog.ReasonText?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            MessageBox.Show("Vui lòng nhập lý do.", "Nhập cân tay", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var hasExisting = sequence == 1 ? _draft.HasCapturedWeight1 : _draft.HasCapturedWeight2;
        if (hasExisting)
        {
            var confirm = MessageBox.Show(
                $"Cân lần {sequence} đã có giá trị. Ghi đè?",
                "Nhập cân tay",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
        }

        SyncDraftFromBindings();
        var result = await _weighTicketService.CaptureManualWeightAsync(
            _draft,
            sequence,
            kg,
            reason,
            StationUserRole.Admin);

        if (!result.Success)
        {
            MessageBox.Show(
                result.ErrorMessage ?? "Không thể nhập cân tay.",
                "Nhập cân tay",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _adminAuthorization.TouchAdminActivity();
        AdminAuditLogger.Write(
            sequence == 1 ? "MANUAL_WEIGH_W1" : "MANUAL_WEIGH_W2",
            "SUCCESS",
            "Admin",
            ticketId: ActiveTicketId?.ToString() ?? "draft",
            reason: reason,
            note: $"kg={kg};seq={sequence}");

        LoadBindingsFromDraft();
        UpdateWeightSourceDisplays();
        UpdateButtonStates();
        UpdateButtonLabels();
        StatusMessage = $"Đã nhập cân lần {sequence}: {kg:N0} kg.";
    }

    private bool EnsureAdminOrNotify(AdminPermission permission, string action)
    {
        var gate = _permissions.EnsurePermission(permission, action);
        if (gate.Allowed)
            return true;

        StatusMessage = gate.ErrorMessage ?? "Cần mở khóa Admin để thực hiện thao tác này.";
        // Avoid MessageBox in headless/unit tests (no MainWindow) — keeps CI non-blocking.
        if (System.Windows.Application.Current?.MainWindow is not null)
        {
            MessageBox.Show(
                StatusMessage,
                "CanXe",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
    }
}
