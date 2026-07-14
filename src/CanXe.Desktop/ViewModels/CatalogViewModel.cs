using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Views.Catalogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class CatalogViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IPrintNotificationService _notificationService;

    public CatalogViewModel(
        ICatalogService catalogService,
        IPrintNotificationService notificationService)
    {
        _catalogService = catalogService;
        _notificationService = notificationService;
    }

    [ObservableProperty] private CatalogTab _selectedTab = CatalogTab.Customer;
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private CatalogRowItem? _selectedRow;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

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

    partial void OnSelectedTabChanged(CatalogTab value)
    {
        OnPropertyChanged(nameof(SelectedTabIndex));
        _ = RefreshAsync();
    }

    public async Task InitializeAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var rows = await _catalogService.ListAsync(SelectedTab, SearchText);
            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);
            StatusMessage = $"Đã tải {rows.Count} mục.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task AddAsync()
    {
        var saved = await ShowEditDialogAsync(null);
        if (saved)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task EditAsync(CatalogRowItem? row)
    {
        row ??= SelectedRow;
        if (row is null)
            return;

        var saved = await ShowEditDialogAsync(row);
        if (saved)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(CatalogRowItem? row)
    {
        row ??= SelectedRow;
        if (row is null)
            return;

        var confirm = MessageBox.Show(
            "Bạn có chắc muốn xóa mục này khỏi danh mục?",
            "Xác nhận xóa",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

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
        }

        await RefreshAsync();
        _ = _notificationService.ShowCatalogDeleteSuccessToastAsync();
    }

    private async Task<bool> ShowEditDialogAsync(CatalogRowItem? row)
    {
        var dialog = new CatalogEditWindow(SelectedTab, row)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true)
            return false;

        return await SaveEditAsync(dialog);
    }

    private async Task<bool> SaveEditAsync(CatalogEditWindow dialog)
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

    public static string FormatCount(int count) =>
        count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public static string FormatPrice(decimal? value) =>
        value is null or <= 0
            ? "—"
            : value.Value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    public static string FormatDate(DateTimeOffset? value) =>
        value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "—";
}
