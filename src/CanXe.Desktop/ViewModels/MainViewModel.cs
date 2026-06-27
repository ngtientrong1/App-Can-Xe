using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Entities;
using CanXe.Domain.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WeighTicketService _weighTicketService;
    private readonly IScaleService _scaleService;

    private WeighTicketDraft _draft = new();

    public MainViewModel(WeighTicketService weighTicketService, IScaleService scaleService)
    {
        _weighTicketService = weighTicketService;
        _scaleService = scaleService;
        _scaleService.WeightChanged += OnScaleWeightChanged;
    }

    [ObservableProperty] private decimal _liveWeightKg;
    [ObservableProperty] private string? _displayNumber;
    [ObservableProperty] private string _ticketDateTimeDisplay = "—";
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private string? _licensePlate;
    [ObservableProperty] private string? _cargoTypeName;
    [ObservableProperty] private string? _unitPriceText;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string _weight1Display = "—";
    [ObservableProperty] private string _weight2Display = "—";
    [ObservableProperty] private string _grossDisplay = "—";
    [ObservableProperty] private string _tareDisplay = "—";
    [ObservableProperty] private string _netDisplay = "—";
    [ObservableProperty] private string _deductionDisplay = "—";
    [ObservableProperty] private string _billableDisplay = "—";
    [ObservableProperty] private string _totalAmountDisplay = "—";
    [ObservableProperty] private string _statusMessage = "Sẵn sàng";
    [ObservableProperty] private bool _isRecordWeightEnabled = true;
    [ObservableProperty] private bool _isManualScaleMode;
    [ObservableProperty] private string? _manualWeightText;

    [ObservableProperty] private DateTime? _filterFromDate;
    [ObservableProperty] private DateTime? _filterToDate;
    [ObservableProperty] private string? _filterCustomerName;
    [ObservableProperty] private string? _filterCargoTypeName;
    [ObservableProperty] private string? _filterLicensePlate;
    [ObservableProperty] private string? _filterDisplayNumber;
    [ObservableProperty] private string? _filterUnitPriceText;

    public ObservableCollection<WeighTicketListItem> Tickets { get; } = [];
    public ObservableCollection<string> CustomerSuggestions { get; } = [];
    public ObservableCollection<string> CargoTypeSuggestions { get; } = [];

    public async Task InitializeAsync()
    {
        await _scaleService.StartAsync();
        LiveWeightKg = await _scaleService.GetCurrentWeightAsync();
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task RecordWeightAsync()
    {
        if (!IsRecordWeightEnabled)
            return;

        var result = await _weighTicketService.RecordWeightAsync(_draft);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Không thể ghi trọng lượng.";
            return;
        }

        IsRecordWeightEnabled = !result.IsRecordingLocked;
        UpdateDisplaysFromDraft();
        StatusMessage = result.IsRecordingLocked
            ? "Đã ghi đủ hai trọng lượng."
            : $"Đã ghi lần cân {result.Event!.Sequence}: {result.Event.WeightKg:N0} kg";

        if (result.Event?.PhotoCaptureSucceeded == false && !string.IsNullOrEmpty(result.Event.PhotoErrorMessage))
            StatusMessage = $"Đã ghi cân. Camera: {result.Event.PhotoErrorMessage}";
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

        if (result.SimilarCustomerWarnings.Count > 0)
            StatusMessage = string.Join(" ", result.SimilarCustomerWarnings);
        else
            StatusMessage = $"Đã lưu phiếu {result.SavedTicket!.DisplayNumber}.";

        ResetDraft();
        await RefreshListAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        ResetDraft();
        StatusMessage = "Đã hủy nhập liệu.";
    }

    [RelayCommand]
    private async Task ApplyFilterAsync() => await RefreshListAsync();

    [RelayCommand]
    private async Task FilterTodayAsync()
    {
        var today = DateTime.Today;
        FilterFromDate = today;
        FilterToDate = today.AddDays(1).AddTicks(-1);
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
    private async Task ContinueTicketAsync(WeighTicketListItem? item)
    {
        if (item is null || item.EventCount >= 2)
            return;

        try
        {
            _draft = await _weighTicketService.LoadTicketForContinuationAsync(item.Id);
            LoadBindingsFromDraft();
            IsRecordWeightEnabled = _draft.Events.Count < 2;
            StatusMessage = $"Đang tiếp tục phiếu {item.DisplayNumber}.";
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

    partial void OnManualWeightTextChanged(string? value)
    {
        if (!IsManualScaleMode || string.IsNullOrWhiteSpace(value))
            return;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var kg))
            _scaleService.SetManualWeightKg(kg);
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
            decimal.TryParse(FilterUnitPriceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
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
            UnitPriceVndPerKg = unitPrice
        };

        var items = await _weighTicketService.GetFilteredAsync(filter);
        Tickets.Clear();
        foreach (var item in items)
            Tickets.Add(item);
    }

    private void ResetDraft()
    {
        _draft = new WeighTicketDraft();
        LoadBindingsFromDraft();
        IsRecordWeightEnabled = true;
    }

    private void SyncDraftFromBindings()
    {
        _draft.CustomerName = CustomerName;
        _draft.LicensePlate = LicensePlate;
        _draft.CargoTypeName = CargoTypeName;
        _draft.Notes = Notes;

        if (string.IsNullOrWhiteSpace(UnitPriceText))
            _draft.UnitPriceVndPerKg = null;
        else if (decimal.TryParse(UnitPriceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var price))
            _draft.UnitPriceVndPerKg = Math.Round(price, 0, MidpointRounding.AwayFromZero);
        else
            _draft.UnitPriceVndPerKg = null;
    }

    private void LoadBindingsFromDraft()
    {
        DisplayNumber = _draft.DisplayNumber ?? "—";
        TicketDateTimeDisplay = _draft.TicketDateTime?.ToString("dd/MM/yyyy HH:mm:ss") ?? DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm:ss");
        CustomerName = _draft.CustomerName;
        LicensePlate = _draft.LicensePlate;
        CargoTypeName = _draft.CargoTypeName;
        UnitPriceText = _draft.UnitPriceVndPerKg?.ToString("N0", CultureInfo.CurrentCulture);
        Notes = _draft.Notes;
        UpdateDisplaysFromDraft();
    }

    private void UpdateDisplaysFromDraft()
    {
        var w1 = _draft.Events.FirstOrDefault(e => e.Sequence == 1)?.WeightKg;
        var w2 = _draft.Events.FirstOrDefault(e => e.Sequence == 2)?.WeightKg;

        Weight1Display = w1?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
        Weight2Display = w2?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";

        var calc = WeightCalculator.Calculate(w1, w2, _draft.UnitPriceVndPerKg);
        GrossDisplay = FormatKg(calc.GrossWeightKg);
        TareDisplay = FormatKg(calc.TareWeightKg);
        NetDisplay = FormatKg(calc.NetWeightKg);
        DeductionDisplay = calc.DeductionWeightKg?.ToString("N3", CultureInfo.CurrentCulture) ?? "—";
        BillableDisplay = FormatKg(calc.BillableWeightKg);
        TotalAmountDisplay = calc.TotalAmountVnd?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
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
