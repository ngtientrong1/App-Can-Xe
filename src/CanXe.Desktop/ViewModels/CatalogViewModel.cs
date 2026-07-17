using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Views.Catalogs;
using CanXe.Infrastructure.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class CatalogViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IPrintNotificationService _notificationService;
    private readonly IUserPermissionService _permissions;
    private readonly IAdminAuthorizationService _adminAuthorization;
    private int _refreshEpoch;

    public CatalogViewModel(
        ICatalogService catalogService,
        IPrintNotificationService notificationService,
        IUserPermissionService permissions,
        IAdminAuthorizationService adminAuthorization)
    {
        _catalogService = catalogService;
        _notificationService = notificationService;
        _permissions = permissions;
        _adminAuthorization = adminAuthorization;
        _adminAuthorization.AdminSessionChanged += (_, _) => ScheduleAdminPermissionRefresh();
    }

    [ObservableProperty] private CatalogTab _selectedTab = CatalogTab.Customer;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private CatalogRowItem? _selectedRow;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isCatalogEditEnabled;
    [ObservableProperty] private string? _catalogAdminHint;

    public int SelectedTabIndex
    {
        get => (int)SelectedTab;
        set
        {
            var tab = (CatalogTab)Math.Clamp(value, 0, 2);
            if (SelectedTab == tab)
                return;
            SelectedTab = tab;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CatalogRowItem> Rows { get; } = [];

    public void NotifyAdminPermissionsChanged()
    {
        IsCatalogEditEnabled = _permissions.HasPermission(AdminPermission.CanAddCatalogItem);
        CatalogAdminHint = IsCatalogEditEnabled
            ? null
            : "Mở khóa Admin để chỉnh sửa danh mục";
        OnPropertyChanged(nameof(IsCatalogEditEnabled));
        OnPropertyChanged(nameof(CatalogAdminHint));
    }

    partial void OnSelectedTabChanged(CatalogTab value)
    {
        OnPropertyChanged(nameof(SelectedTabIndex));
        _ = RefreshAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            NotifyAdminPermissionsChanged();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            // Never bubble catalog init failures to DispatcherUnhandledException.
            AppExceptionLogger.WriteError("Catalog.InitializeAsync", ex);
            StatusMessage = "Không thể tải danh mục.";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var epoch = Interlocked.Increment(ref _refreshEpoch);
        IsBusy = true;
        try
        {
            var tab = SelectedTab;
            var search = SearchText;
            var rows = await _catalogService.ListAsync(tab, search) ?? Array.Empty<CatalogRowItem>();
            if (epoch != _refreshEpoch)
                return;

            await RunOnUiAsync(() =>
            {
                Rows.Clear();
                foreach (var row in rows)
                    Rows.Add(row);
                StatusMessage = $"Đã tải {rows.Count} mục.";
            });
        }
        catch (Exception ex)
        {
            AppExceptionLogger.WriteError("Catalog.RefreshAsync", ex, $"tab={SelectedTab}");
            StatusMessage = "Không thể tải danh mục.";
        }
        finally
        {
            if (epoch == _refreshEpoch)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task AddAsync()
    {
        if (!EnsureCatalogPermission(AdminPermission.CanAddCatalogItem, "CATALOG_ADD"))
            return;

        var outcome = await ShowEditDialogAsync(null);
        if (outcome == CatalogEditOutcome.Saved)
        {
            AdminAuditLogger.Write("CATALOG_ADD", "SUCCESS", "Admin", note: SelectedTab.ToString());
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task EditAsync(CatalogRowItem? row)
    {
        if (!EnsureCatalogPermission(AdminPermission.CanEditCatalogItem, "CATALOG_EDIT"))
            return;

        row ??= SelectedRow;
        if (row is null)
            return;

        var outcome = await ShowEditDialogAsync(row);
        if (outcome == CatalogEditOutcome.Saved)
        {
            AdminAuditLogger.Write(
                "CATALOG_EDIT",
                "SUCCESS",
                "Admin",
                note: $"{SelectedTab}:{row.Id}");
            await RefreshAsync();
        }
        else if (outcome == CatalogEditOutcome.Deleted)
        {
            // Delete already refreshed + toasted inside PerformDeleteAsync.
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(CatalogRowItem? row)
    {
        row ??= SelectedRow;
        if (row is null)
            return;

        await PerformDeleteAsync(row, confirm: true);
    }

    private async Task<bool> PerformDeleteAsync(CatalogRowItem row, bool confirm)
    {
        if (!EnsureDeletePermission())
            return false;

        if (confirm)
        {
            var confirmText = GetDeleteConfirmText(row.Tab);
            var answer = MessageBox.Show(
                confirmText,
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
                return false;
        }

        try
        {
            switch (row.Tab)
            {
                case CatalogTab.Customer:
                    await _catalogService.HideCustomerAsync(row.Id);
                    break;
                case CatalogTab.Vehicle:
                    await _catalogService.HideVehicleAsync(row.Id);
                    break;
                case CatalogTab.CargoType:
                    await _catalogService.HideCargoTypeAsync(row.Id);
                    break;
                default:
                    return false;
            }

            AdminAuditLogger.Write(
                GetDeleteAuditAction(row.Tab),
                "SUCCESS",
                "Admin",
                note: $"id={row.Id}");
            await RefreshAsync();
            _ = _notificationService.ShowCatalogDeleteSuccessToastAsync(GetDeleteToast(row.Tab));
            return true;
        }
        catch (Exception ex)
        {
            AppExceptionLogger.WriteError("Catalog.DeleteAsync", ex, $"tab={row.Tab};id={row.Id}");
            StatusMessage = "Không thể xóa mục danh mục.";
            return false;
        }
    }

    private bool EnsureDeletePermission()
    {
        if (_permissions.HasPermission(AdminPermission.CanDeleteCatalogItem))
        {
            _permissions.EnsurePermission(AdminPermission.CanDeleteCatalogItem, "CATALOG_DELETE");
            return true;
        }

        AdminAuditLogger.Write(
            "PERMISSION_DENIED_DELETE_CATALOG",
            "DENIED",
            _permissions.CurrentRole.ToString(),
            note: SelectedTab.ToString());

        MessageBox.Show(
            "Cần mở khóa Admin để thực hiện thao tác này.",
            "CanXe",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private bool EnsureCatalogPermission(AdminPermission permission, string action)
    {
        var gate = _permissions.EnsurePermission(permission, action);
        if (gate.Allowed)
            return true;

        MessageBox.Show(
            gate.ErrorMessage ?? "Cần mở khóa Admin để thực hiện thao tác này.",
            "CanXe",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private async Task<CatalogEditOutcome> ShowEditDialogAsync(CatalogRowItem? row)
    {
        var dialog = new CatalogEditWindow(
            SelectedTab,
            row,
            allowDelete: row is not null && IsCatalogEditEnabled)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
            return CatalogEditOutcome.Cancelled;

        if (dialog.DeleteRequested)
        {
            if (row is null)
                return CatalogEditOutcome.Cancelled;

            var deleted = await PerformDeleteAsync(row, confirm: true);
            return deleted ? CatalogEditOutcome.Deleted : CatalogEditOutcome.Cancelled;
        }

        var saved = await SaveEditAsync(dialog);
        return saved ? CatalogEditOutcome.Saved : CatalogEditOutcome.Cancelled;
    }

    private async Task<bool> SaveEditAsync(CatalogEditWindow dialog)
    {
        try
        {
            CatalogSaveResult result = SelectedTab switch
            {
                CatalogTab.Customer => await _catalogService.SaveCustomerAsync(dialog.ToCustomerEdit()),
                CatalogTab.Vehicle => await _catalogService.SaveVehicleAsync(dialog.ToVehicleEdit()),
                CatalogTab.CargoType => await _catalogService.SaveCargoTypeAsync(dialog.ToCargoEdit()),
                _ => CatalogSaveResult.Fail("Tab không hợp lệ.")
            };

            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage ?? "Không thể lưu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            AppExceptionLogger.WriteError("Catalog.SaveEditAsync", ex, $"tab={SelectedTab}");
            MessageBox.Show("Không thể lưu danh mục.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void ScheduleAdminPermissionRefresh()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        if (dispatcher.CheckAccess())
            NotifyAdminPermissionsChanged();
        else
            _ = dispatcher.BeginInvoke(NotifyAdminPermissionsChanged);
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private static string GetDeleteConfirmText(CatalogTab tab) => tab switch
    {
        CatalogTab.Customer => "Xóa khách hàng này khỏi danh mục?",
        CatalogTab.Vehicle => "Xóa biển số này khỏi danh mục?",
        CatalogTab.CargoType => "Xóa loại hàng này khỏi danh mục?",
        _ => "Xóa mục này khỏi danh mục?"
    };

    private static string GetDeleteToast(CatalogTab tab) => tab switch
    {
        CatalogTab.Customer => "Đã xóa khách hàng",
        CatalogTab.Vehicle => "Đã xóa biển số",
        CatalogTab.CargoType => "Đã xóa loại hàng",
        _ => "Đã xóa khỏi danh mục"
    };

    private static string GetDeleteAuditAction(CatalogTab tab) => tab switch
    {
        CatalogTab.Customer => "DELETE_CUSTOMER",
        CatalogTab.Vehicle => "DELETE_VEHICLE",
        CatalogTab.CargoType => "DELETE_CARGO_TYPE",
        _ => "CATALOG_DELETE"
    };

    public static string FormatCount(int count) =>
        count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public static string FormatPrice(decimal? value) =>
        value is null or <= 0
            ? "—"
            : value.Value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public static string FormatDate(DateTimeOffset? value) =>
        value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "—";

    private enum CatalogEditOutcome
    {
        Cancelled,
        Saved,
        Deleted
    }
}
