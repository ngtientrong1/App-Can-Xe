using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CanXe.Application.Configuration;
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
    private readonly FastEntrySearchService _fastEntrySearch;
    private readonly IScaleService _scaleService;
    private readonly IUiFocusService _focusService;
    private readonly AppSettings _settings;

    private WeighTicketDraft _draft = new();
    private CancellationTokenSource? _customerSearchCts;
    private CancellationTokenSource? _vehicleSearchCts;
    private CancellationTokenSource? _cargoSearchCts;
    private CancellationTokenSource? _notesSearchCts;
    private string? _activeQuickRangeLabel;

    public MainViewModel(
        WeighTicketService weighTicketService,
        FastEntrySearchService fastEntrySearch,
        IScaleService scaleService,
        IUiFocusService focusService,
        AppSettings settings)
    {
        _weighTicketService = weighTicketService;
        _fastEntrySearch = fastEntrySearch;
        _scaleService = scaleService;
        _focusService = focusService;
        _settings = settings;
        _scaleService.WeightChanged += OnScaleWeightChanged;
        IsDeveloperPanelAvailable = string.Equals(settings.DeviceMode, "Simulation", StringComparison.OrdinalIgnoreCase)
                                   && settings.ShowDeveloperPanel;
    }

    [ObservableProperty] private decimal _liveWeightKg;
    [ObservableProperty] private string _displayNumber = "—";
    [ObservableProperty] private bool _isPreviewDisplayNumber = true;
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
    [ObservableProperty] private string _scaleStabilityText = "● ỔN ĐỊNH";
    [ObservableProperty] private string _statusMessage = "Sẵn sàng";
    [ObservableProperty] private bool _isWeigh1Enabled = true;
    [ObservableProperty] private bool _isWeigh2Enabled = true;
    [ObservableProperty] private bool _isContinuationMode;
    [ObservableProperty] private bool _isManualScaleMode;
    [ObservableProperty] private bool _isCameraPanelVisible;
    [ObservableProperty] private bool _isDevPanelExpanded;
    [ObservableProperty] private bool _isDeveloperPanelAvailable;
    [ObservableProperty] private bool _developerWeight1OverrideEnabled;
    [ObservableProperty] private bool _isWeight1DevOverrideWarningVisible;
    [ObservableProperty] private string? _manualWeightText;
    [ObservableProperty] private bool _isVehicleContextVisible;
    [ObservableProperty] private string? _vehicleContextCustomerText;
    [ObservableProperty] private string? _vehicleContextFrequentCargoText;
    [ObservableProperty] private string? _vehicleContextLastUsedText;
    [ObservableProperty] private string? _vehicleContextCustomerPreview;
    [ObservableProperty] private string? _vehicleContextCargoPreview;
    [ObservableProperty] private bool _isApplyBothPrimary;
    [ObservableProperty] private bool _isAdvancedFilterVisible = true;
    [ObservableProperty] private string _footerTicketCount = "0";
    [ObservableProperty] private string _footerTotalNet = "0 kg";
    [ObservableProperty] private string _footerTotalAmount = "0 VNĐ";

    [ObservableProperty] private string _weigh1ButtonText = "LẤY CÂN LẦN 1";
    [ObservableProperty] private string _weigh2ButtonText = "LẤY CÂN LẦN 2";

    [ObservableProperty] private DateTime? _filterFromDate;
    [ObservableProperty] private DateTime? _filterToDate;
    [ObservableProperty] private string? _filterCustomerName;
    [ObservableProperty] private string? _filterCargoTypeName;
    [ObservableProperty] private string? _filterLicensePlate;
    [ObservableProperty] private string? _filterDisplayNumber;
    [ObservableProperty] private string? _filterUnitPriceText;
    [ObservableProperty] private string? _filterUnitPriceToText;
    [ObservableProperty] private string _filterResultSummary = string.Empty;
    [ObservableProperty] private string _filterTotalsSummary = string.Empty;

    private VehicleUsageContext? _activeVehicleContext;

    public GridLength WeighColumnWidth =>
        new(WorkAreaLayoutCalculator.GetColumnStars(IsCameraPanelVisible).WeighStars, GridUnitType.Star);

    public GridLength InfoColumnWidth =>
        new(WorkAreaLayoutCalculator.GetColumnStars(IsCameraPanelVisible).InfoStars, GridUnitType.Star);

    public GridLength CameraColumnWidth =>
        new(WorkAreaLayoutCalculator.GetColumnStars(IsCameraPanelVisible).CameraStars, GridUnitType.Star);

    public int CameraColumnMinWidth => IsCameraPanelVisible ? 220 : 0;

    partial void OnIsAdvancedFilterVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(AdvancedFilterToggleText));

    public string AdvancedFilterToggleText =>
        IsAdvancedFilterVisible ? "ẨN BỘ LỌC NÂNG CAO" : "MỞ BỘ LỌC NÂNG CAO";

    public ObservableCollection<WeighTicketListItem> Tickets { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> CustomerSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> CargoTypeSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> VehicleSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> NotesSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> FilterCustomerSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> FilterCargoSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> FilterVehicleSuggestions { get; } = [];
    public ObservableCollection<ActiveFilterChip> ActiveFilterChips { get; } = [];

    public async Task InitializeAsync()
    {
        await _scaleService.StartAsync();
        LiveWeightKg = await _scaleService.GetCurrentWeightAsync();
        await RefreshPreviewDisplayNumberAsync();
        LoadBindingsFromDraft();
        await FilterTodayAsync();
    }

    [RelayCommand]
    private async Task CaptureWeight1Async() => await CaptureWeightAsync(1);

    [RelayCommand]
    private async Task CaptureWeight2Async() => await CaptureWeightAsync(2);

    private async Task CaptureWeightAsync(int sequence)
    {
        _draft.DeveloperWeight1OverrideEnabled = DeveloperWeight1OverrideEnabled;
        var result = await _weighTicketService.CaptureWeightAsync(_draft, sequence);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Không thể lấy cân.";
            return;
        }

        UpdateDisplaysFromDraft();
        UpdateButtonStates();
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

        await ResetDraftAsync();
        await RefreshListAsync();
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        _weighTicketService.CancelDraft(_draft);
        await ResetDraftAsync();
        StatusMessage = "Đã hủy nhập liệu.";
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private void ToggleCameraPanel()
    {
        IsCameraPanelVisible = !IsCameraPanelVisible;
        NotifyWorkAreaLayoutChanged();
    }

    partial void OnIsCameraPanelVisibleChanged(bool value) => NotifyWorkAreaLayoutChanged();

    private void NotifyWorkAreaLayoutChanged()
    {
        OnPropertyChanged(nameof(WeighColumnWidth));
        OnPropertyChanged(nameof(InfoColumnWidth));
        OnPropertyChanged(nameof(CameraColumnWidth));
        OnPropertyChanged(nameof(CameraColumnMinWidth));
    }

    [RelayCommand]
    private void ToggleAdvancedFilter() => IsAdvancedFilterVisible = !IsAdvancedFilterVisible;

    [RelayCommand]
    private void ApplyVehicleContextBoth() => ApplyVehicleContext(VehicleContextApplyMode.Both);

    [RelayCommand]
    private void ApplyVehicleContextCustomer() => ApplyVehicleContext(VehicleContextApplyMode.CustomerOnly);

    [RelayCommand]
    private void ApplyVehicleContextCargo() => ApplyVehicleContext(VehicleContextApplyMode.CargoOnly);

    public void ApplyVehicleContextFromKeyboard()
    {
        if (IsApplyBothPrimary)
            ApplyVehicleContext(VehicleContextApplyMode.Both);
    }

    [RelayCommand]
    private void ToggleDevPanel() => IsDevPanelExpanded = !IsDevPanelExpanded;

    [RelayCommand]
    private void PrintTicket() => StatusMessage = "In phiếu sẽ có ở giai đoạn sau.";

    [RelayCommand]
    private void ExportExcelStub() => StatusMessage = "Xuất Excel sẽ có ở giai đoạn sau.";

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        _activeQuickRangeLabel = null;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task FilterTodayAsync() => await ApplyQuickRangeAsync("Hôm nay", DateTime.Today, DateTime.Today);

    [RelayCommand]
    private async Task FilterYesterdayAsync()
    {
        var day = DateTime.Today.AddDays(-1);
        await ApplyQuickRangeAsync("Hôm qua", day, day);
    }

    [RelayCommand]
    private async Task FilterLast7DaysAsync() =>
        await ApplyQuickRangeAsync("7 ngày", DateTime.Today.AddDays(-6), DateTime.Today);

    [RelayCommand]
    private async Task FilterThisMonthAsync()
    {
        var today = DateTime.Today;
        await ApplyQuickRangeAsync("Tháng này", new DateTime(today.Year, today.Month, 1), today);
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
        FilterUnitPriceToText = null;
        _activeQuickRangeLabel = null;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task RemoveFilterChipAsync(ActiveFilterChip? chip)
    {
        if (chip is null)
            return;

        switch (chip.Key)
        {
            case "quickRange":
                _activeQuickRangeLabel = null;
                FilterFromDate = null;
                FilterToDate = null;
                break;
            case "dateRange":
                FilterFromDate = null;
                FilterToDate = null;
                break;
            case "customer":
                FilterCustomerName = null;
                break;
            case "cargo":
                FilterCargoTypeName = null;
                break;
            case "plate":
                FilterLicensePlate = null;
                break;
            case "ticketNo":
                FilterDisplayNumber = null;
                break;
            case "unitPrice":
            case "unitPriceRange":
                FilterUnitPriceText = null;
                FilterUnitPriceToText = null;
                break;
        }

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
    public async Task ContinueTicketAsync(WeighTicketListItem? item)
    {
        if (item is null || item.EventCount >= 2)
            return;

        try
        {
            _draft = await _weighTicketService.LoadTicketForContinuationAsync(item.Id);
            LoadBindingsFromDraft();
            IsContinuationMode = true;
            IsPreviewDisplayNumber = false;
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
    private void SetManualScale8500() => SetManualWeight(8500m);

    [RelayCommand]
    private void SetManualScale18500() => SetManualWeight(18500m);

    partial void OnIsManualScaleModeChanged(bool value)
    {
        _scaleService.SetManualMode(value);
        ScaleStabilityText = value ? "● THỦ CÔNG" : "● ỔN ĐỊNH";
        StatusMessage = value ? "Chế độ cân thủ công." : "Chế độ cân random.";
    }

    partial void OnDeveloperWeight1OverrideEnabledChanged(bool value)
    {
        IsWeight1DevOverrideWarningVisible = value && IsDeveloperPanelAvailable;
        _draft.DeveloperWeight1OverrideEnabled = value;
        UpdateButtonStates();
        UpdateButtonLabels();
    }

    partial void OnCustomerNameChanged(string? value) => _ = DebouncedSearchAsync(
        () => _customerSearchCts, cts => _customerSearchCts = cts, SearchCustomersAsync);

    partial void OnCargoTypeNameChanged(string? value) => _ = DebouncedSearchAsync(
        () => _cargoSearchCts, cts => _cargoSearchCts = cts, SearchCargoTypesAsync);

    partial void OnNotesChanged(string? value) => _ = DebouncedSearchAsync(
        () => _notesSearchCts, cts => _notesSearchCts = cts, SearchNotesAsync);

    partial void OnLicensePlateChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value) && value != value.ToUpperInvariant())
        {
            LicensePlate = value.ToUpperInvariant();
            return;
        }

        _ = DebouncedSearchAsync(() => _vehicleSearchCts, cts => _vehicleSearchCts = cts, SearchVehiclesAsync);
        _ = UpdateVehicleContextAsync();
    }

    partial void OnFilterCustomerNameChanged(string? value) =>
        _ = DebouncedFilterSearchAsync(FilterCustomerSuggestions, value, term => _fastEntrySearch.SearchCustomersAsync(term));

    partial void OnFilterCargoTypeNameChanged(string? value) =>
        _ = DebouncedFilterSearchAsync(FilterCargoSuggestions, value, term => _fastEntrySearch.SearchCargoTypesAsync(term));

    partial void OnFilterLicensePlateChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value) && value != value.ToUpperInvariant())
        {
            FilterLicensePlate = value.ToUpperInvariant();
            return;
        }

        _ = DebouncedFilterSearchAsync(FilterVehicleSuggestions, value,
            term => _fastEntrySearch.SearchVehiclesAsync(term, FilterCustomerName));
    }

    partial void OnUnitPriceTextChanged(string? value)
    {
        _draft.DraftUnitPrice = ParseUnitPrice(value);
        UpdateDisplaysFromDraft();
    }

    partial void OnManualWeightTextChanged(string? value)
    {
        if (!IsManualScaleMode || string.IsNullOrWhiteSpace(value))
            return;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var kg))
            _scaleService.SetManualWeightKg(kg);
    }

    public void OnSuggestionCommitted(AutocompleteField field, AutocompleteSuggestionItem? suggestion, string currentText)
    {
        var text = suggestion is { IsNewEntryOption: true } or null
            ? currentText.Trim()
            : suggestion!.PrimaryText;

        switch (field)
        {
            case AutocompleteField.Customer:
                CustomerName = text;
                _focusService.FocusVehicleField();
                break;
            case AutocompleteField.Vehicle:
                LicensePlate = text.ToUpperInvariant();
                _ = UpdateVehicleContextAsync();
                if (IsVehicleContextVisible)
                    _focusService.FocusVehicleContextCard();
                else
                    _focusService.FocusCargoTypeField();
                break;
            case AutocompleteField.CargoType:
                CargoTypeName = text;
                _focusService.FocusUnitPriceField();
                break;
            case AutocompleteField.Notes:
                if (suggestion is not null && !suggestion.IsNewEntryOption)
                    Notes = text;
                break;
        }
    }

    public async Task OnVehicleCommittedAsync(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return;

        LicensePlate = plate.ToUpperInvariant();
        await UpdateVehicleContextAsync();
    }

    private async Task UpdateVehicleContextAsync()
    {
        ClearVehicleContextUi();

        if (string.IsNullOrWhiteSpace(LicensePlate))
            return;

        var context = await _weighTicketService.GetVehicleUsageContextAsync(LicensePlate);
        if (context is null)
            return;

        _activeVehicleContext = context;
        IsVehicleContextVisible = true;
        VehicleContextCustomerText = context.RecentCustomerName is { } customer
            ? $"Khách gần nhất: {customer}"
            : null;
        VehicleContextFrequentCargoText = context.FrequentCargoTypeName is { } cargo
            ? $"Loại hàng thường dùng: {cargo}"
            : null;
        VehicleContextLastUsedText = context.LastUsedAt?.ToString("dd/MM/yyyy");
        VehicleContextCustomerPreview = VehicleUsageContextApplier.BuildCustomerChangePreview(context, CustomerName);
        VehicleContextCargoPreview = VehicleUsageContextApplier.BuildCargoChangePreview(context, CargoTypeName);
        IsApplyBothPrimary = VehicleUsageContextApplier.ShouldHighlightApplyBoth(CustomerName, CargoTypeName);
    }

    private void ClearVehicleContextUi()
    {
        _activeVehicleContext = null;
        IsVehicleContextVisible = false;
        VehicleContextCustomerText = null;
        VehicleContextFrequentCargoText = null;
        VehicleContextLastUsedText = null;
        VehicleContextCustomerPreview = null;
        VehicleContextCargoPreview = null;
        IsApplyBothPrimary = false;
    }

    private void ApplyVehicleContext(VehicleContextApplyMode mode)
    {
        if (_activeVehicleContext is not { } context)
            return;

        var applied = VehicleUsageContextApplier.Apply(context, mode, CustomerName, CargoTypeName);

        if (applied.CustomerName is not null)
        {
            CustomerName = applied.CustomerName;
            _draft.DraftCustomerId = applied.CustomerId;
            _draft.DraftCustomer = applied.CustomerName;
        }

        if (applied.CargoTypeName is not null)
        {
            CargoTypeName = applied.CargoTypeName;
            _draft.DraftCargoTypeId = applied.CargoTypeId;
            _draft.DraftCargoType = applied.CargoTypeName;
        }

        ClearVehicleContextUi();

        if (mode is VehicleContextApplyMode.CargoOnly ||
            (mode is VehicleContextApplyMode.Both && !string.IsNullOrWhiteSpace(CargoTypeName)))
            _focusService.FocusUnitPriceField();
        else
            _focusService.FocusCargoTypeField();
    }

    private async Task ApplyQuickRangeAsync(string label, DateTime from, DateTime to)
    {
        _activeQuickRangeLabel = label;
        FilterFromDate = from;
        FilterToDate = to;
        await RefreshListAsync();
    }

    private async Task DebouncedSearchAsync(
        Func<CancellationTokenSource?> getCts,
        Action<CancellationTokenSource?> setCts,
        Func<Task> searchAction)
    {
        getCts()?.Cancel();
        var cts = new CancellationTokenSource();
        setCts(cts);
        var token = cts.Token;
        try
        {
            await Task.Delay(200, token);
            await searchAction();
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task DebouncedFilterSearchAsync(
        ObservableCollection<AutocompleteSuggestionItem> target,
        string? value,
        Func<string, Task<IReadOnlyList<AutocompleteSuggestionItem>>> search)
    {
        try
        {
            await Task.Delay(200);
            var items = await search(value ?? string.Empty);
            target.Clear();
            foreach (var item in items.Where(i => !i.IsNewEntryOption))
                target.Add(item);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task SearchCustomersAsync()
    {
        CustomerSuggestions.Clear();
        var items = await _fastEntrySearch.SearchCustomersAsync(CustomerName ?? string.Empty);
        foreach (var item in items)
            CustomerSuggestions.Add(item);
    }

    private async Task SearchCargoTypesAsync()
    {
        CargoTypeSuggestions.Clear();
        var items = await _fastEntrySearch.SearchCargoTypesAsync(CargoTypeName ?? string.Empty);
        foreach (var item in items)
            CargoTypeSuggestions.Add(item);
    }

    private async Task SearchVehiclesAsync()
    {
        VehicleSuggestions.Clear();
        var items = await _fastEntrySearch.SearchVehiclesAsync(LicensePlate ?? string.Empty, CustomerName);
        foreach (var item in items)
            VehicleSuggestions.Add(item);
    }

    private async Task SearchNotesAsync()
    {
        NotesSuggestions.Clear();
        if (string.IsNullOrWhiteSpace(Notes))
            return;

        var items = await _fastEntrySearch.SearchNotesAsync(Notes);
        foreach (var item in items)
            NotesSuggestions.Add(item);
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
        var filter = BuildCurrentFilter();
        var result = await _weighTicketService.GetFilteredWithSummaryAsync(filter);

        Tickets.Clear();
        foreach (var item in result.Items)
            Tickets.Add(item);

        ActiveFilterChips.Clear();
        foreach (var chip in ActiveFilterChipBuilder.Build(filter, _activeQuickRangeLabel))
            ActiveFilterChips.Add(chip);

        FilterResultSummary = ActiveFilterChips.Count > 0
            ? $"Đang hiển thị: {result.Count} phiếu"
            : string.Empty;
        FilterTotalsSummary = string.Empty;
        FooterTicketCount = result.Count.ToString(CultureInfo.CurrentCulture);
        FooterTotalNet = $"{result.TotalNetWeightKg:N0} kg";
        FooterTotalAmount = $"{result.TotalAmountVnd:N0} VNĐ";
    }

    private WeighTicketFilter BuildCurrentFilter()
    {
        decimal? exactPrice = null;
        decimal? fromPrice = null;
        decimal? toPrice = null;

        decimal? parsedFrom = null;
        if (!string.IsNullOrWhiteSpace(FilterUnitPriceText) &&
            decimal.TryParse(FilterUnitPriceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var fp) &&
            fp > 0)
            parsedFrom = fp;

        decimal? parsedTo = null;
        if (!string.IsNullOrWhiteSpace(FilterUnitPriceToText) &&
            decimal.TryParse(FilterUnitPriceToText, NumberStyles.Number, CultureInfo.CurrentCulture, out var tp) &&
            tp > 0)
            parsedTo = tp;

        if (parsedFrom.HasValue && parsedTo.HasValue)
        {
            fromPrice = parsedFrom;
            toPrice = parsedTo;
        }
        else if (parsedFrom.HasValue)
            exactPrice = parsedFrom;

        return new WeighTicketFilter
        {
            FromDate = FilterFromDate.HasValue ? new DateTimeOffset(FilterFromDate.Value.Date) : null,
            ToDate = FilterToDate.HasValue
                ? new DateTimeOffset(FilterToDate.Value.Date.AddDays(1).AddTicks(-1))
                : null,
            CustomerName = FilterCustomerName,
            CargoTypeName = FilterCargoTypeName,
            LicensePlate = FilterLicensePlate,
            DisplayNumber = FilterDisplayNumber,
            UnitPriceVndPerKg = exactPrice,
            UnitPriceFromVndPerKg = fromPrice,
            UnitPriceToVndPerKg = toPrice,
            MaxResults = 200
        };
    }

    private async Task ResetDraftAsync()
    {
        _draft = new WeighTicketDraft();
        DeveloperWeight1OverrideEnabled = false;
        IsContinuationMode = false;
        LoadBindingsFromDraft();
        await RefreshPreviewDisplayNumberAsync();
        UpdateButtonStates();
        UpdateButtonLabels();
    }

    private async Task RefreshPreviewDisplayNumberAsync()
    {
        if (_draft.ExistingTicketId.HasValue)
        {
            DisplayNumber = _draft.DisplayNumber ?? "—";
            IsPreviewDisplayNumber = false;
            return;
        }

        DisplayNumber = await _weighTicketService.GetPreviewDisplayNumberAsync();
        IsPreviewDisplayNumber = true;
    }

    private void SyncDraftFromBindings()
    {
        _draft.DraftCustomer = CustomerName;
        _draft.DraftVehicle = LicensePlate;
        _draft.DraftCargoType = CargoTypeName;
        _draft.DraftNotes = Notes;
        _draft.DraftUnitPrice = ParseUnitPrice(UnitPriceText);
        _draft.DeveloperWeight1OverrideEnabled = DeveloperWeight1OverrideEnabled;
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
        if (_draft.ExistingTicketId.HasValue)
        {
            DisplayNumber = _draft.DisplayNumber ?? "—";
            IsPreviewDisplayNumber = false;
        }

        TicketDateTimeDisplay = _draft.TicketDateTime?.ToString("dd/MM/yyyy HH:mm:ss")
            ?? DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm:ss");
        CustomerName = _draft.DraftCustomer;
        LicensePlate = _draft.DraftVehicle;
        CargoTypeName = _draft.DraftCargoType;
        UnitPriceText = _draft.DraftUnitPrice?.ToString("N0", CultureInfo.CurrentCulture);
        Notes = _draft.DraftNotes;
        DeveloperWeight1OverrideEnabled = _draft.DeveloperWeight1OverrideEnabled;
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
        IsServiceWeighVisible = calc.NetWeightKg.HasValue && !WeightCalculator.HasBillableUnitPrice(unitPrice);
    }

    private void UpdateButtonStates()
    {
        _draft.DeveloperWeight1OverrideEnabled = DeveloperWeight1OverrideEnabled;
        IsWeigh1Enabled = DraftWorkflowRules.CanUpdateWeight1(
            _draft.IsWeight1LockedFromSavedTicket,
            _draft.DraftWeight2.HasValue,
            DeveloperWeight1OverrideEnabled);
        IsWeigh2Enabled = DraftWorkflowRules.CanUpdateWeight2(_draft.IsWeight2LockedFromSavedTicket);
    }

    private void UpdateButtonLabels()
    {
        if (_draft.IsWeight1LockedFromSavedTicket)
            Weigh1ButtonText = "CÂN LẦN 1 (ĐÃ LƯU)";
        else if (!IsWeigh1Enabled && _draft.DraftWeight2.HasValue)
            Weigh1ButtonText = "CÂN LẦN 1 ĐÃ KHÓA";
        else
            Weigh1ButtonText = _draft.DraftWeight1.HasValue ? "CẬP NHẬT CÂN LẦN 1" : "LẤY CÂN LẦN 1";

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

public enum AutocompleteField
{
    Customer,
    Vehicle,
    CargoType,
    Notes
}
