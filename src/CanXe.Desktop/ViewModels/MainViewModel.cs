using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WeighTicketService _weighTicketService;
    private readonly IScaleService _scaleService;
    private readonly IUiFocusService _focusService;

    private WeighTicketDraft _draft = new();

    public MainViewModel(
        WeighTicketService weighTicketService,
        IScaleService scaleService,
        IUiFocusService focusService)
    {
        _weighTicketService = weighTicketService;
        _scaleService = scaleService;
        _focusService = focusService;
        _scaleService.WeightChanged += OnScaleWeightChanged;
    }

    [ObservableProperty] private decimal _liveWeightKg;
    [ObservableProperty] private string _displayNumber = "Tự động khi lưu";
    [ObservableProperty] private string _ticketDateTimeDisplay = "—";
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private string? _licensePlate;
    [ObservableProperty] private string? _cargoTypeName;
    [ObservableProperty] private string? _unitPriceText;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private bool _weight1HasValue;
    [ObservableProperty] private bool _weight2HasValue;
    [ObservableProperty] private string _weight1ValueText = "Chưa lấy cân";
    [ObservableProperty] private string _weight1TimeText = string.Empty;
    [ObservableProperty] private string _weight2ValueText = "Chưa lấy cân";
    [ObservableProperty] private string _weight2TimeText = string.Empty;
    [ObservableProperty] private string _grossDisplay = "—";
    [ObservableProperty] private string _tareDisplay = "—";
    [ObservableProperty] private string _netDisplay = "—";
    [ObservableProperty] private string _deductionDisplay = "—";
    [ObservableProperty] private string _billableDisplay = "—";
    [ObservableProperty] private string _totalAmountDisplay = "—";
    [ObservableProperty] private bool _isServiceWeighVisible;
    [ObservableProperty] private string _statusMessage = "Sẵn sàng";
    [ObservableProperty] private bool _isWeigh1Enabled = true;
    [ObservableProperty] private bool _isWeigh2Enabled = true;
    [ObservableProperty] private bool _isContinuationMode;
    [ObservableProperty] private bool _isManualScaleMode;
    [ObservableProperty] private bool _isCameraPanelVisible = true;
    [ObservableProperty] private string? _manualWeightText;
    [ObservableProperty] private string? _vehicleSuggestionText;

    [ObservableProperty] private string _weigh1ButtonText = "LẤY CÂN LẦN 1";
    [ObservableProperty] private string _weigh2ButtonText = "LẤY CÂN LẦN 2";

    [ObservableProperty] private DateTime? _filterFromDate;
    [ObservableProperty] private DateTime? _filterToDate;
    [ObservableProperty] private string? _filterCustomerName;
    [ObservableProperty] private string? _filterCargoTypeName;
    [ObservableProperty] private string? _filterLicensePlate;
    [ObservableProperty] private string? _filterDisplayNumber;
    [ObservableProperty] private string? _filterUnitPriceText;

    public GridLength InfoColumnWidth => new(IsCameraPanelVisible ? 53 : 73, GridUnitType.Star);
    public GridLength CameraColumnWidth => new(IsCameraPanelVisible ? 20 : 0, GridUnitType.Star);

    public ObservableCollection<WeighTicketListItem> Tickets { get; } = [];
    public ObservableCollection<string> CustomerSuggestions { get; } = [];
    public ObservableCollection<string> CargoTypeSuggestions { get; } = [];

    public async Task InitializeAsync()
    {
        await _scaleService.StartAsync();
        LiveWeightKg = await _scaleService.GetCurrentWeightAsync();
        await FilterTodayAsync();
    }

    [RelayCommand]
    private async Task CaptureWeight1Async() => await CaptureWeightAsync(1);

    [RelayCommand]
    private async Task CaptureWeight2Async() => await CaptureWeightAsync(2);

    private async Task CaptureWeightAsync(int sequence)
    {
        var result = await _weighTicketService.CaptureWeightAsync(_draft, sequence);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Không thể lấy cân.";
            return;
        }

        UpdateDisplaysFromDraft();
        UpdateButtonLabels();
        StatusMessage = result.IsUpdate
            ? $"Đã cập nhật cân lần {sequence}: {result.WeightKg:N0} kg"
            : $"Đã lấy cân lần {sequence}: {result.WeightKg:N0} kg";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        SyncDraftFromBindings();
        var result = await _weighTicketService.SaveAsync(_draft);

        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Lưu thất bại.";
            return;
        }

        StatusMessage = result.SimilarCustomerWarnings.Count > 0
            ? string.Join(" ", result.SimilarCustomerWarnings)
            : $"Đã lưu phiếu {result.SavedTicket!.DisplayNumber}.";

        ResetDraft();
        await RefreshListAsync();
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private void Cancel()
    {
        _weighTicketService.CancelDraft(_draft);
        ResetDraft();
        StatusMessage = "Đã hủy nhập liệu.";
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private void ToggleCameraPanel() => IsCameraPanelVisible = !IsCameraPanelVisible;

    [RelayCommand]
    private void PrintTicket() =>
        StatusMessage = "In phiếu sẽ có ở giai đoạn sau.";

    [RelayCommand]
    private async Task ApplyFilterAsync() => await RefreshListAsync();

    [RelayCommand]
    private async Task FilterTodayAsync()
    {
        var today = DateTime.Today;
        FilterFromDate = today;
        FilterToDate = today;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task FilterYesterdayAsync()
    {
        var day = DateTime.Today.AddDays(-1);
        FilterFromDate = day;
        FilterToDate = day;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task FilterLast7DaysAsync()
    {
        FilterFromDate = DateTime.Today.AddDays(-6);
        FilterToDate = DateTime.Today;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task FilterThisMonthAsync()
    {
        var today = DateTime.Today;
        FilterFromDate = new DateTime(today.Year, today.Month, 1);
        FilterToDate = today;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterFromDate = null;
        FilterToDate = null;
        FilterCustomerName = null;
        FilterCargoTypeName = null;
        FilterLicensePlate = null;
        FilterDisplayNumber = null;
        FilterUnitPriceText = null;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task OpenTicketDetailAsync(WeighTicketListItem? item)
    {
        if (item is null)
            return;

        try
        {
            var detail = await _weighTicketService.GetTicketDetailAsync(item.Id);
            var owner = System.Windows.Application.Current.MainWindow;
            if (owner is not null)
                TicketDetailViewModel.Show(owner, detail);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ContinueTicketAsync(WeighTicketListItem? item)
    {
        if (item is null || item.EventCount >= 2)
            return;

        try
        {
            _draft = await _weighTicketService.LoadTicketForContinuationAsync(item.Id);
            LoadBindingsFromDraft();
            IsContinuationMode = true;
            UpdateButtonStates();
            UpdateButtonLabels();
            StatusMessage = $"Đang tiếp tục phiếu {item.DisplayNumber}. Chỉ có thể lấy lần cân còn thiếu.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SearchCustomersAsync()
    {
        CustomerSuggestions.Clear();
        var results = await _weighTicketService.SearchCustomersAsync(CustomerName ?? string.Empty);
        foreach (var c in results)
            CustomerSuggestions.Add(c.Name);
    }

    [RelayCommand]
    private async Task SearchCargoTypesAsync()
    {
        CargoTypeSuggestions.Clear();
        var results = await _weighTicketService.SearchCargoTypesAsync(CargoTypeName ?? string.Empty);
        foreach (var c in results)
            CargoTypeSuggestions.Add(c.Name);
    }

    [RelayCommand]
    private void SetManualScale8500() => SetManualWeight(8500m);

    [RelayCommand]
    private void SetManualScale18500() => SetManualWeight(18500m);

    partial void OnIsManualScaleModeChanged(bool value)
    {
        _scaleService.SetManualMode(value);
        StatusMessage = value ? "Chế độ cân thủ công." : "Chế độ cân random.";
    }

    partial void OnCustomerNameChanged(string? value) => _ = SearchCustomersAsync();

    partial void OnCargoTypeNameChanged(string? value) => _ = SearchCargoTypesAsync();

    partial void OnUnitPriceTextChanged(string? value)
    {
        _draft.DraftUnitPrice = ParseUnitPrice(value);
        UpdateDisplaysFromDraft();
    }

    partial void OnIsCameraPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(InfoColumnWidth));
        OnPropertyChanged(nameof(CameraColumnWidth));
    }

    partial void OnLicensePlateChanged(string? value) => _ = UpdateVehicleSuggestionAsync();

    partial void OnManualWeightTextChanged(string? value)
    {
        if (!IsManualScaleMode || string.IsNullOrWhiteSpace(value))
            return;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var kg))
            _scaleService.SetManualWeightKg(kg);
    }

    private async Task UpdateVehicleSuggestionAsync()
    {
        VehicleSuggestionText = null;
        if (string.IsNullOrWhiteSpace(LicensePlate))
            return;

        var suggestion = await _weighTicketService.GetVehicleSuggestionAsync(LicensePlate);
        if (suggestion?.LastCustomerName is { } name)
            VehicleSuggestionText = $"Gợi ý khách trước: {name} (không tự đổi)";
    }

    private void SetManualWeight(decimal kg)
    {
        IsManualScaleMode = true;
        ManualWeightText = kg.ToString("N0", CultureInfo.CurrentCulture);
        _scaleService.SetManualWeightKg(kg);
    }

    private void OnScaleWeightChanged(object? sender, decimal weightKg)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => LiveWeightKg = weightKg);
    }

    private async Task RefreshListAsync()
    {
        decimal? unitPrice = null;
        if (!string.IsNullOrWhiteSpace(FilterUnitPriceText) &&
            decimal.TryParse(FilterUnitPriceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) &&
            parsed > 0)
            unitPrice = parsed;

        var filter = new WeighTicketFilter
        {
            FromDate = FilterFromDate.HasValue
                ? new DateTimeOffset(FilterFromDate.Value.Date)
                : null,
            ToDate = FilterToDate.HasValue
                ? new DateTimeOffset(FilterToDate.Value.Date.AddDays(1).AddTicks(-1))
                : null,
            CustomerName = FilterCustomerName,
            CargoTypeName = FilterCargoTypeName,
            LicensePlate = FilterLicensePlate,
            DisplayNumber = FilterDisplayNumber,
            UnitPriceVndPerKg = unitPrice,
            MaxResults = 200
        };

        var items = await _weighTicketService.GetFilteredAsync(filter);
        Tickets.Clear();
        foreach (var item in items)
            Tickets.Add(item);
    }

    private void ResetDraft()
    {
        _draft = new WeighTicketDraft();
        IsContinuationMode = false;
        LoadBindingsFromDraft();
        UpdateButtonStates();
        UpdateButtonLabels();
    }

    private void SyncDraftFromBindings()
    {
        _draft.DraftCustomer = CustomerName;
        _draft.DraftVehicle = LicensePlate;
        _draft.DraftCargoType = CargoTypeName;
        _draft.DraftNotes = Notes;
        _draft.DraftUnitPrice = ParseUnitPrice(UnitPriceText);
    }

    private static decimal? ParseUnitPrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var price))
            return null;

        price = Math.Round(price, 0, MidpointRounding.AwayFromZero);
        return price > 0 ? price : null;
    }

    private void LoadBindingsFromDraft()
    {
        DisplayNumber = _draft.ExistingTicketId.HasValue
            ? _draft.DisplayNumber ?? "—"
            : "Tự động khi lưu";
        TicketDateTimeDisplay = _draft.TicketDateTime?.ToString("dd/MM/yyyy HH:mm:ss")
            ?? DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm:ss");
        CustomerName = _draft.DraftCustomer;
        LicensePlate = _draft.DraftVehicle;
        CargoTypeName = _draft.DraftCargoType;
        UnitPriceText = _draft.DraftUnitPrice?.ToString("N0", CultureInfo.CurrentCulture);
        Notes = _draft.DraftNotes;
        UpdateDisplaysFromDraft();
    }

    private void UpdateDisplaysFromDraft()
    {
        if (_draft.DraftWeight1 is { } w1)
        {
            Weight1HasValue = true;
            Weight1ValueText = $"{w1:N0} kg";
            Weight1TimeText = _draft.DraftWeight1RecordedAt?.ToString("HH:mm:ss") ?? string.Empty;
        }
        else
        {
            Weight1HasValue = false;
            Weight1ValueText = "Chưa lấy cân";
            Weight1TimeText = string.Empty;
        }

        if (_draft.DraftWeight2 is { } w2)
        {
            Weight2HasValue = true;
            Weight2ValueText = $"{w2:N0} kg";
            Weight2TimeText = _draft.DraftWeight2RecordedAt?.ToString("HH:mm:ss") ?? string.Empty;
        }
        else
        {
            Weight2HasValue = false;
            Weight2ValueText = "Chưa lấy cân";
            Weight2TimeText = string.Empty;
        }

        var unitPrice = _draft.DraftUnitPrice;
        var calc = WeightCalculator.Calculate(_draft.DraftWeight1, _draft.DraftWeight2, unitPrice);
        GrossDisplay = FormatKg(calc.GrossWeightKg);
        TareDisplay = FormatKg(calc.TareWeightKg);
        NetDisplay = FormatKg(calc.NetWeightKg);
        DeductionDisplay = calc.DeductionWeightKg?.ToString("N3", CultureInfo.CurrentCulture) ?? "—";
        BillableDisplay = FormatKg(calc.BillableWeightKg);
        TotalAmountDisplay = calc.TotalAmountVnd?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
        IsServiceWeighVisible = _draft.DraftWeight1.HasValue && _draft.DraftWeight2.HasValue &&
                              !WeightCalculator.HasBillableUnitPrice(unitPrice);
    }

    private void UpdateButtonStates()
    {
        IsWeigh1Enabled = !_draft.IsWeight1LockedFromSavedTicket;
        IsWeigh2Enabled = !_draft.IsWeight2LockedFromSavedTicket;
    }

    private void UpdateButtonLabels()
    {
        Weigh1ButtonText = _draft.DraftWeight1.HasValue && !_draft.IsWeight1LockedFromSavedTicket
            ? "CẬP NHẬT CÂN LẦN 1"
            : _draft.IsWeight1LockedFromSavedTicket ? "CÂN LẦN 1 (ĐÃ LƯU)" : "LẤY CÂN LẦN 1";

        Weigh2ButtonText = _draft.DraftWeight2.HasValue && !_draft.IsWeight2LockedFromSavedTicket
            ? "CẬP NHẬT CÂN LẦN 2"
            : _draft.IsWeight2LockedFromSavedTicket ? "CÂN LẦN 2 (ĐÃ LƯU)" : "LẤY CÂN LẦN 2";
    }

    private static string FormatKg(decimal? kg) =>
        kg?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";

    public async ValueTask DisposeAsync()
    {
        _scaleService.WeightChanged -= OnScaleWeightChanged;
        await _scaleService.StopAsync();
        if (_scaleService is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }
}
