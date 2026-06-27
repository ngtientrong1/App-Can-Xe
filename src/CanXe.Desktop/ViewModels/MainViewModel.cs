using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
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
    private readonly ITicketDocumentRenderer _ticketDocumentRenderer;
    private readonly AppSettings _settings;
    private readonly AppPaths _appPaths;

    private WeighTicketDraft _draft = new();
    private CancellationTokenSource? _customerSearchCts;
    private CancellationTokenSource? _vehicleSearchCts;
    private CancellationTokenSource? _cargoSearchCts;
    private CancellationTokenSource? _notesSearchCts;
    private string? _activeQuickRangeLabel;
    private string? _lastAutoFilledPlate;
    private bool _customerEditedAfterAutoFill;
    private bool _cargoEditedAfterAutoFill;
    private bool _suppressAutoFillEditTracking;
    private WeighTicketDetailDto? _lastSavedTicketDetail;
    private CancellationTokenSource? _toastCts;

    public MainViewModel(
        WeighTicketService weighTicketService,
        FastEntrySearchService fastEntrySearch,
        IScaleService scaleService,
        IUiFocusService focusService,
        ITicketDocumentRenderer ticketDocumentRenderer,
        AppSettings settings,
        SettingsViewModel settingsViewModel,
        AppPaths appPaths)
    {
        _weighTicketService = weighTicketService;
        _fastEntrySearch = fastEntrySearch;
        _scaleService = scaleService;
        _focusService = focusService;
        _ticketDocumentRenderer = ticketDocumentRenderer;
        _settings = settings;
        _appPaths = appPaths;
        Settings = settingsViewModel;
        Settings.StationSettingsSaved += (_, dto) => OnStationSettingsSaved(dto);
        _scaleService.WeightChanged += OnScaleWeightChanged;
        IsDeveloperPanelAvailable = string.Equals(settings.DeviceMode, "Simulation", StringComparison.OrdinalIgnoreCase)
                                   && settings.ShowDeveloperPanel;
        IsDeveloperWeightUnlockVisible = IsDeveloperPanelAvailable && settings.DeveloperTicketEditEnabled;
        foreach (var (code, label) in WeightOverrideReasons.All)
            WeightOverrideReasonOptions.Add(new WeightOverrideReasonOption(code, label));
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
    [ObservableProperty] private ScaleInputMode _scaleInputMode = ScaleInputMode.SimulationAutomatic;
    [ObservableProperty] private bool _isCameraDrawerOpen;
    [ObservableProperty] private bool _isDevDrawerOpen;
    [ObservableProperty] private bool _suppressAutocomplete;
    [ObservableProperty] private bool _isCompactMode;
    [ObservableProperty] private AppNavigationSection _activeSection = AppNavigationSection.WeighTicket;
    [ObservableProperty] private string _simulatedWeightText = "18500";
    [ObservableProperty] private bool _simulateScaleStable = true;
    [ObservableProperty] private bool _simulateScaleDisconnected;
    [ObservableProperty] private bool _simulateCameraError;
    [ObservableProperty] private bool _simulateScaleError;

    public SettingsViewModel Settings { get; }

    public bool IsWeighTicketSectionVisible => ActiveSection == AppNavigationSection.WeighTicket;
    public bool IsCatalogSectionVisible => ActiveSection == AppNavigationSection.Catalog;
    public bool IsDeviceSectionVisible => ActiveSection == AppNavigationSection.Device;
    public bool IsReportSectionVisible => ActiveSection == AppNavigationSection.Report;
    public bool IsSettingsSectionVisible => ActiveSection == AppNavigationSection.Settings;
    public bool IsSystemSectionVisible => ActiveSection == AppNavigationSection.System;
    public bool IsUtilityRailVisible => IsWeighTicketSectionVisible;
    public bool IsDevDrawerAvailable => IsDeveloperPanelAvailable && IsWeighTicketSectionVisible;

    partial void OnActiveSectionChanged(AppNavigationSection value)
    {
        OnPropertyChanged(nameof(IsWeighTicketSectionVisible));
        OnPropertyChanged(nameof(IsCatalogSectionVisible));
        OnPropertyChanged(nameof(IsDeviceSectionVisible));
        OnPropertyChanged(nameof(IsReportSectionVisible));
        OnPropertyChanged(nameof(IsSettingsSectionVisible));
        OnPropertyChanged(nameof(IsSystemSectionVisible));
        OnPropertyChanged(nameof(IsUtilityRailVisible));
        OnPropertyChanged(nameof(IsDevDrawerAvailable));
        OnPropertyChanged(nameof(IsNavSystemActive));
        OnPropertyChanged(nameof(IsNavWeighTicketActive));
        OnPropertyChanged(nameof(IsNavCatalogActive));
        OnPropertyChanged(nameof(IsNavDeviceActive));
        OnPropertyChanged(nameof(IsNavReportActive));
        OnPropertyChanged(nameof(IsNavSettingsActive));
    }

    partial void OnIsDevDrawerOpenChanged(bool value) => OnPropertyChanged(nameof(IsDevDrawerAvailable));
    [ObservableProperty] private bool _isDeveloperPanelAvailable;
    [ObservableProperty] private bool _developerWeight1OverrideEnabled;
    [ObservableProperty] private bool _isWeight1DevOverrideWarningVisible;
    [ObservableProperty] private string? _manualWeightText;
    [ObservableProperty] private bool _isAdvancedFilterVisible;
    [ObservableProperty] private string _footerTicketCount = "0";
    [ObservableProperty] private string _footerTotalNet = "0 kg";
    [ObservableProperty] private string _footerTotalBillable = "0 kg";
    [ObservableProperty] private string _footerTotalAmount = "0 VNĐ";
    [ObservableProperty] private string _footerMissingPriceCount = "0";
    [ObservableProperty] private bool _isToastVisible;
    [ObservableProperty] private string? _toastMessage;
    [ObservableProperty] private bool _isTicketPreviewVisible;
    [ObservableProperty] private WeighTicketDetailDto? _previewTicketDetail;
    [ObservableProperty] private bool _isPreviewFrontSide = true;
    [ObservableProperty] private string _headerScaleStatus = "● Đầu cân: Ổn định";
    [ObservableProperty] private string _headerCameraStatus = "● Camera: Đã kết nối";
    [ObservableProperty] private string _headerClockText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    [ObservableProperty] private string? _lastSavedTicketDisplayNumber;

    [ObservableProperty] private bool _isEditingExistingTicket;
    [ObservableProperty] private long? _editingTicketId;
    [ObservableProperty] private string? _editingTicketNumber;
    [ObservableProperty] private bool _isDeveloperWeightUnlockVisible;
    [ObservableProperty] private bool _isDevWeightEditUnlocked;
    [ObservableProperty] private string? _devWeight1Text;
    [ObservableProperty] private string? _devWeight2Text;
    [ObservableProperty] private string? _weightOverrideReasonCode;
    [ObservableProperty] private string? _weightOverrideReasonOther;
    [ObservableProperty] private bool _isWeightOverrideReasonPanelVisible;
    [ObservableProperty] private bool _isManualWeightOverrideMessageVisible;
    [ObservableProperty] private int? _highlightedTicketId;
    [ObservableProperty] private WeighTicketListItem? _selectedTicket;
    [ObservableProperty] private bool _canLoadTicketIntoForm = true;

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

    public GridLength WeighColumnWidth =>
        new(WorkAreaLayoutCalculator.GetMainColumnStars(IsCameraDrawerOpen).WeighStars, GridUnitType.Star);

    public GridLength InfoColumnWidth =>
        new(WorkAreaLayoutCalculator.GetMainColumnStars(IsCameraDrawerOpen).InfoStars, GridUnitType.Star);

    public GridLength CameraDrawerColumnWidth =>
        IsCameraDrawerOpen
            ? new GridLength(WorkAreaLayoutCalculator.CameraDrawerWidthPixels)
            : new GridLength(0);

    public GridLength UtilityRailColumnWidth =>
        new GridLength(WorkAreaLayoutCalculator.GetUtilityRailWidth(IsCompactMode));

    public double LiveWeightFontSize => IsCompactMode ? 72 : 96;

    public bool IsSimulationAutomaticMode => ScaleInputMode == ScaleInputMode.SimulationAutomatic;
    public bool IsSimulationManualMode => ScaleInputMode == ScaleInputMode.SimulationManual;
    public bool IsReturnToAutomaticVisible => ScaleInputMode == ScaleInputMode.SimulationManual;
    public bool IsManualWeightInputVisible => ScaleInputMode == ScaleInputMode.SimulationManual;
    public bool IsHardwareModeEnabled => false;

    public string WeightSourceText => ScaleInputModeDisplay.GetWeightSourceText(ScaleInputMode);

    public bool IsNavSystemActive => ActiveSection == AppNavigationSection.System;
    public bool IsNavWeighTicketActive => ActiveSection == AppNavigationSection.WeighTicket;
    public bool IsNavCatalogActive => ActiveSection == AppNavigationSection.Catalog;
    public bool IsNavDeviceActive => ActiveSection == AppNavigationSection.Device;
    public bool IsNavReportActive => ActiveSection == AppNavigationSection.Report;
    public bool IsNavSettingsActive => ActiveSection == AppNavigationSection.Settings;

    public string? ActiveQuickFilterKey => _activeQuickRangeLabel;

    partial void OnIsAdvancedFilterVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(AdvancedFilterToggleText));

    public string AdvancedFilterToggleText =>
        IsAdvancedFilterVisible ? "ẨN BỘ LỌC" : "HIỆN BỘ LỌC";

    public ObservableCollection<WeighTicketListItem> Tickets { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> CustomerSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> CargoTypeSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> VehicleSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> NotesSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> FilterCustomerSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> FilterCargoSuggestions { get; } = [];
    public ObservableCollection<AutocompleteSuggestionItem> FilterVehicleSuggestions { get; } = [];
    public ObservableCollection<ActiveFilterChip> ActiveFilterChips { get; } = [];
    public ObservableCollection<WeightOverrideReasonOption> WeightOverrideReasonOptions { get; } = [];

    public bool IsCreateModeActionsVisible => !IsEditingExistingTicket;
    public bool IsEditModeActionsVisible => IsEditingExistingTicket;
    public string EditBannerTitle =>
        IsEditingExistingTicket && !string.IsNullOrWhiteSpace(EditingTicketNumber)
            ? $"ĐANG CHỈNH SỬA PHIẾU {EditingTicketNumber}"
            : string.Empty;

    partial void OnIsEditingExistingTicketChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCreateModeActionsVisible));
        OnPropertyChanged(nameof(IsEditModeActionsVisible));
        OnPropertyChanged(nameof(EditBannerTitle));
        UpdateButtonStates();
    }

    partial void OnEditingTicketNumberChanged(string? value) =>
        OnPropertyChanged(nameof(EditBannerTitle));

    public async Task InitializeAsync()
    {
        await _scaleService.StartAsync();
        LiveWeightKg = await _scaleService.GetCurrentWeightAsync();
        IsCompactMode = CompactLayoutPolicy.ShouldUseCompactMode(SystemParameters.PrimaryScreenWidth);
        if (IsCompactMode)
        {
            IsCameraDrawerOpen = false;
            IsAdvancedFilterVisible = false;
        }

        ApplyScaleInputMode(ScaleInputModeDisplay.GetDefaultMode(_settings.DeviceMode), userInitiated: false);
        await Settings.LoadAsync(_appPaths.DatabasePath);
        HeaderClockText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
        _ = RunClockAsync();
        await RefreshPreviewDisplayNumberAsync();
        LoadBindingsFromDraft();
        await FilterTodayAsync();
    }

    [RelayCommand]
    private void Navigate(AppNavigationSection section) => ActiveSection = section;

    [RelayCommand]
    private void ToggleDevDrawer() => IsDevDrawerOpen = !IsDevDrawerOpen;

    [RelayCommand]
    private void SelectSimulationAutomaticMode() =>
        ApplyScaleInputMode(ScaleInputMode.SimulationAutomatic, userInitiated: true);

    [RelayCommand]
    private void SelectSimulationManualMode() =>
        ApplyScaleInputMode(ScaleInputMode.SimulationManual, userInitiated: true);

    [RelayCommand]
    private void ReturnToAutomaticMode() =>
        ApplyScaleInputMode(ScaleInputMode.SimulationAutomatic, userInitiated: true);

    private void ApplyScaleInputMode(ScaleInputMode mode, bool userInitiated)
    {
        ScaleInputMode = mode;
        switch (mode)
        {
            case ScaleInputMode.SimulationAutomatic:
                _scaleService.ResumeAutomaticSimulation();
                _scaleService.SetManualMode(false);
                if (userInitiated)
                    StatusMessage = "Đã chuyển về chế độ tự động mô phỏng.";
                break;
            case ScaleInputMode.SimulationManual:
                _scaleService.SetManualMode(true);
                if (userInitiated)
                    StatusMessage = "Đang dùng nguồn DEV thủ công.";
                break;
            case ScaleInputMode.Hardware:
                break;
        }

        UpdateScaleStatusDisplay();
        OnPropertyChanged(nameof(IsSimulationAutomaticMode));
        OnPropertyChanged(nameof(IsSimulationManualMode));
        OnPropertyChanged(nameof(IsReturnToAutomaticVisible));
        OnPropertyChanged(nameof(IsManualWeightInputVisible));
        OnPropertyChanged(nameof(WeightSourceText));
    }

    private void UpdateScaleStatusDisplay()
    {
        HeaderScaleStatus = ScaleInputModeDisplay.GetHeaderScaleBadge(ScaleInputMode, SimulateScaleDisconnected);
        ScaleStabilityText = SimulateScaleDisconnected
            ? "● MẤT KẾT NỐI"
            : SimulateScaleStable ? "● ỔN ĐỊNH" : "● ĐANG THAY ĐỔI";
    }

    partial void OnSimulateScaleStableChanged(bool value) => UpdateScaleStatusDisplay();
    partial void OnSimulateScaleDisconnectedChanged(bool value) => UpdateScaleStatusDisplay();
    partial void OnScaleInputModeChanged(ScaleInputMode value) => UpdateScaleStatusDisplay();

    [RelayCommand]
    private void ApplySimulatedWeight()
    {
        if (decimal.TryParse(SimulatedWeightText, NumberStyles.Number, CultureInfo.CurrentCulture, out var kg))
            SetManualWeight(kg);
    }

    [RelayCommand]
    private void ApplyPresetWeight8500() => SetManualWeight(8500);

    [RelayCommand]
    private void ApplyPresetWeight18500() => SetManualWeight(18500);

    private void OnStationSettingsSaved(StationSettingsDto dto)
    {
        StatusMessage = "Thông tin trạm cân đã cập nhật — preview phiếu dùng dữ liệu mới.";
    }

    private TicketDocumentRenderOptions BuildPreviewRenderOptions() => Settings.BuildPreviewOptions();

    private async Task RunClockAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            HeaderClockText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
        }
    }

    [RelayCommand]
    private async Task CaptureWeight1Async() => await CaptureWeightAsync(1);

    [RelayCommand]
    private async Task CaptureWeight2Async() => await CaptureWeightAsync(2);

    private async Task CaptureWeightAsync(int sequence)
    {
        if (IsEditingExistingTicket)
        {
            StatusMessage = "Không thể lấy cân khi đang chỉnh sửa phiếu.";
            return;
        }

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
        var filter = BuildCurrentFilter();
        var result = await _weighTicketService.SaveAsync(_draft, filter);

        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Lưu thất bại.";
            return;
        }

        var displayNumber = result.SavedTicket!.DisplayNumber;
        var savedId = result.SavedTicket.Id;
        _lastSavedTicketDetail = await _weighTicketService.GetTicketDetailAsync(savedId);
        LastSavedTicketDisplayNumber = displayNumber;

        await ResetDraftAsync();
        await RefreshListAsync();

        if (result.IsVisibleInCurrentFilter)
        {
            if (result.SimilarCustomerWarnings.Count > 0)
                StatusMessage = string.Join(" ", result.SimilarCustomerWarnings);
            else
                StatusMessage = $"Đã lưu phiếu {displayNumber}.";

            await FlashHighlightTicketAsync(savedId, scrollToTop: true);
        }
        else
        {
            StatusMessage =
                $"Đã lưu phiếu {displayNumber} nhưng phiếu không nằm trong bộ lọc hiện tại.";
        }

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
    private void ToggleCameraPanel() => ToggleCameraDrawer();

    [RelayCommand]
    private void ToggleCameraDrawer() => IsCameraDrawerOpen = !IsCameraDrawerOpen;

    partial void OnIsCameraDrawerOpenChanged(bool value) => NotifyWorkAreaLayoutChanged();

    private void NotifyWorkAreaLayoutChanged()
    {
        OnPropertyChanged(nameof(WeighColumnWidth));
        OnPropertyChanged(nameof(InfoColumnWidth));
        OnPropertyChanged(nameof(CameraDrawerColumnWidth));
        OnPropertyChanged(nameof(UtilityRailColumnWidth));
        OnPropertyChanged(nameof(LiveWeightFontSize));
    }

    [RelayCommand]
    private void ToggleAdvancedFilter() => IsAdvancedFilterVisible = !IsAdvancedFilterVisible;

    [RelayCommand]
    private void PrintTicket() => StatusMessage = "In phiếu Brother sẽ có ở giai đoạn sau.";

    [RelayCommand]
    private async Task ViewTicketAsync()
    {
        try
        {
            WeighTicketDetailDto? detail = null;
            if (IsEditingExistingTicket && EditingTicketId is long editId)
                detail = await _weighTicketService.GetTicketDetailAsync((int)editId);
            else if (SelectedTicket is { } selected)
                detail = await _weighTicketService.GetTicketDetailAsync(selected.Id);
            else if (_lastSavedTicketDetail is not null)
                detail = _lastSavedTicketDetail;

            if (detail is null)
            {
                StatusMessage = "Chọn một phiếu hoặc lưu phiếu trước khi xem.";
                return;
            }

            PreviewTicketDetail = detail;
            IsPreviewFrontSide = true;
            IsTicketPreviewVisible = true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void CloseTicketPreview() => IsTicketPreviewVisible = false;

    [RelayCommand]
    private void ShowPreviewFront() => IsPreviewFrontSide = true;

    [RelayCommand]
    private void ShowPreviewBack() => IsPreviewFrontSide = false;

    [RelayCommand]
    private async Task CopyTicketImageAsync()
    {
        if (PreviewTicketDetail is not { } detail)
            return;

        try
        {
            var rendered = _ticketDocumentRenderer.RenderCombinedVertical(detail, BuildPreviewRenderOptions());
            ClipboardImageService.CopyPngToClipboard(rendered.PngBytes);
            await ShowToastAsync("Đã sao chép ảnh phiếu");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveTicketImageAsync()
    {
        if (PreviewTicketDetail is not { } detail)
            return;

        try
        {
            var rendered = _ticketDocumentRenderer.RenderCombinedVertical(detail, BuildPreviewRenderOptions());
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = rendered.SuggestedFileName,
                Filter = "PNG (*.png)|*.png"
            };
            if (dialog.ShowDialog() != true)
                return;

            await File.WriteAllBytesAsync(dialog.FileName, rendered.PngBytes);
            StatusMessage = $"Đã lưu ảnh phiếu: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void ExportExcelStub() => StatusMessage = "Xuất Excel sẽ có ở giai đoạn sau.";

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        _activeQuickRangeLabel = null;
        OnPropertyChanged(nameof(ActiveQuickFilterKey));
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
        OnPropertyChanged(nameof(ActiveQuickFilterKey));
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
    private async Task BeginEditTicketAsync(WeighTicketListItem? item)
    {
        if (item is null || !CanLoadTicketIntoForm || IsEditingExistingTicket)
            return;

        try
        {
            _draft = await _weighTicketService.LoadTicketForEditAsync(item.Id);
            IsEditingExistingTicket = true;
            EditingTicketId = item.Id;
            EditingTicketNumber = item.DisplayNumber;
            CanLoadTicketIntoForm = false;
            IsContinuationMode = false;
            IsDevWeightEditUnlocked = false;
            WeightOverrideReasonCode = null;
            WeightOverrideReasonOther = null;
            DevWeight1Text = _draft.DraftWeight1?.ToString("N0", CultureInfo.CurrentCulture);
            DevWeight2Text = _draft.DraftWeight2?.ToString("N0", CultureInfo.CurrentCulture);
            LoadBindingsFromDraft();
            UpdateWeightOverrideUi();
            UpdateButtonStates();
            UpdateButtonLabels();
            StatusMessage = $"Đang chỉnh sửa phiếu {item.DisplayNumber}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task UpdateTicketAsync()
    {
        SyncDraftFromBindings();
        _draft.IsEditMode = true;
        _draft.DeveloperWeightUnlockEnabled = IsDevWeightEditUnlocked;
        _draft.WeightOverrideReasonCode = WeightOverrideReasonCode;
        _draft.WeightOverrideReasonOther = WeightOverrideReasonOther;

        var result = await _weighTicketService.UpdateTicketAsync(_draft, "desktop-user");

        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Cập nhật thất bại.";
            return;
        }

        var updatedId = result.UpdatedTicket!.Id;
        var displayNumber = result.UpdatedTicket.DisplayNumber;
        await ResetDraftAsync();
        await RefreshListAsync();
        await FlashHighlightTicketAsync(updatedId, scrollToTop: false);
        StatusMessage = $"Đã cập nhật phiếu {displayNumber}.";
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private async Task ExitEditAsync()
    {
        _weighTicketService.CancelDraft(_draft);
        await ResetDraftAsync();
        StatusMessage = "Đã thoát chế độ chỉnh sửa.";
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private void UnlockDevWeightEdit()
    {
        if (!IsDeveloperWeightUnlockVisible || !IsEditingExistingTicket)
            return;

        IsDevWeightEditUnlocked = true;
        _draft.DeveloperWeightUnlockEnabled = true;
        DevWeight1Text = _draft.DraftWeight1?.ToString("N0", CultureInfo.CurrentCulture);
        DevWeight2Text = _draft.DraftWeight2?.ToString("N0", CultureInfo.CurrentCulture);
        UpdateWeightOverrideUi();
        StatusMessage = "DEV: đã mở khóa sửa trọng lượng.";
    }

    [RelayCommand]
    private async Task OpenTicketDetailAsync(WeighTicketListItem? item) =>
        await BeginEditTicketAsync(item);

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

    partial void OnDeveloperWeight1OverrideEnabledChanged(bool value)
    {
        IsWeight1DevOverrideWarningVisible = value && IsDeveloperPanelAvailable;
        _draft.DeveloperWeight1OverrideEnabled = value;
        UpdateButtonStates();
        UpdateButtonLabels();
    }

    partial void OnDevWeight1TextChanged(string? value)
    {
        if (!IsDevWeightEditUnlocked)
            return;

        _draft.DraftWeight1 = ParseDevWeight(value);
        UpdateDisplaysFromDraft();
        UpdateWeightOverrideUi();
    }

    partial void OnDevWeight2TextChanged(string? value)
    {
        if (!IsDevWeightEditUnlocked)
            return;

        _draft.DraftWeight2 = ParseDevWeight(value);
        UpdateDisplaysFromDraft();
        UpdateWeightOverrideUi();
    }

    partial void OnWeightOverrideReasonCodeChanged(string? value) => UpdateWeightOverrideUi();

    partial void OnCustomerNameChanged(string? value)
    {
        if (!_suppressAutoFillEditTracking && !string.IsNullOrWhiteSpace(_lastAutoFilledPlate))
            _customerEditedAfterAutoFill = true;
        _ = DebouncedSearchAsync(() => _customerSearchCts, cts => _customerSearchCts = cts, SearchCustomersAsync);
    }

    partial void OnCargoTypeNameChanged(string? value)
    {
        if (!_suppressAutoFillEditTracking && !string.IsNullOrWhiteSpace(_lastAutoFilledPlate))
            _cargoEditedAfterAutoFill = true;
        _ = DebouncedSearchAsync(() => _cargoSearchCts, cts => _cargoSearchCts = cts, SearchCargoTypesAsync);
    }

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
        if (ScaleInputMode != ScaleInputMode.SimulationManual || string.IsNullOrWhiteSpace(value))
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
                _ = OnVehiclePlateCommittedAsync(text.ToUpperInvariant());
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

    public async Task OnVehiclePlateCommittedAsync(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return;

        LicensePlate = plate.ToUpperInvariant();
        await AutoFillFromVehicleHistoryAsync(LicensePlate);
    }

    private async Task AutoFillFromVehicleHistoryAsync(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return;

        var normalizedPlate = plate.Trim().ToUpperInvariant();
        if (normalizedPlate == _lastAutoFilledPlate &&
            (_customerEditedAfterAutoFill || _cargoEditedAfterAutoFill))
            return;

        if (normalizedPlate != _lastAutoFilledPlate)
        {
            _customerEditedAfterAutoFill = false;
            _cargoEditedAfterAutoFill = false;
        }

        var context = await _weighTicketService.GetVehicleUsageContextAsync(normalizedPlate);
        if (context is null)
            return;

        var applied = VehicleUsageContextApplier.Apply(
            context,
            VehicleContextApplyMode.Both,
            CustomerName,
            CargoTypeName);

        var filledAny = false;
        _suppressAutoFillEditTracking = true;
        SuppressAutocomplete = true;
        try
        {
            if (applied.CustomerName is not null &&
                (! _customerEditedAfterAutoFill || normalizedPlate != _lastAutoFilledPlate))
            {
                CustomerName = applied.CustomerName;
                _draft.DraftCustomerId = applied.CustomerId;
                _draft.DraftCustomer = applied.CustomerName;
                filledAny = true;
            }

            if (applied.CargoTypeName is not null &&
                (! _cargoEditedAfterAutoFill || normalizedPlate != _lastAutoFilledPlate))
            {
                CargoTypeName = applied.CargoTypeName;
                _draft.DraftCargoTypeId = applied.CargoTypeId;
                _draft.DraftCargoType = applied.CargoTypeName;
                filledAny = true;
            }
        }
        finally
        {
            _suppressAutoFillEditTracking = false;
            SuppressAutocomplete = false;
        }

        _lastAutoFilledPlate = normalizedPlate;

        if (filledAny)
            await ShowToastAsync("Đã tự điền khách hàng và loại hàng theo lịch sử xe");
    }

    private async Task ShowToastAsync(string message)
    {
        ToastMessage = message;
        IsToastVisible = true;
        _toastCts?.Cancel();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;
        try
        {
            await Task.Delay(2500, token);
            IsToastVisible = false;
            ToastMessage = null;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task ApplyQuickRangeAsync(string label, DateTime from, DateTime to)
    {
        _activeQuickRangeLabel = label;
        FilterFromDate = from;
        FilterToDate = to;
        OnPropertyChanged(nameof(ActiveQuickFilterKey));
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

    private static void FillSuggestions(ObservableCollection<AutocompleteSuggestionItem> target, IEnumerable<AutocompleteSuggestionItem> items)
    {
        target.Clear();
        foreach (var item in items.Take(AutocompleteDropDownPolicy.DefaultMaxVisibleItems))
            target.Add(item);
    }

    private async Task SearchCustomersAsync()
    {
        var items = await _fastEntrySearch.SearchCustomersAsync(CustomerName ?? string.Empty);
        FillSuggestions(CustomerSuggestions, items);
    }

    private async Task SearchCargoTypesAsync()
    {
        var items = await _fastEntrySearch.SearchCargoTypesAsync(CargoTypeName ?? string.Empty);
        FillSuggestions(CargoTypeSuggestions, items);
    }

    private async Task SearchVehiclesAsync()
    {
        var items = await _fastEntrySearch.SearchVehiclesAsync(LicensePlate ?? string.Empty, CustomerName);
        FillSuggestions(VehicleSuggestions, items);
    }

    private async Task SearchNotesAsync()
    {
        if (string.IsNullOrWhiteSpace(Notes))
        {
            NotesSuggestions.Clear();
            return;
        }

        var items = await _fastEntrySearch.SearchNotesAsync(Notes);
        FillSuggestions(NotesSuggestions, items);
    }

    private void SetManualWeight(decimal kg)
    {
        if (ScaleInputMode != ScaleInputMode.SimulationManual)
            ApplyScaleInputMode(ScaleInputMode.SimulationManual, userInitiated: true);

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
        FooterTotalBillable = $"{result.TotalBillableWeightKg:N0} kg";
        FooterTotalAmount = $"{result.TotalAmountVnd:N0} VNĐ";
        FooterMissingPriceCount = result.MissingPriceCount.ToString(CultureInfo.CurrentCulture);
    }

    partial void OnIsCompactModeChanged(bool value) => NotifyWorkAreaLayoutChanged();

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
        IsEditingExistingTicket = false;
        EditingTicketId = null;
        EditingTicketNumber = null;
        IsDevWeightEditUnlocked = false;
        DevWeight1Text = null;
        DevWeight2Text = null;
        WeightOverrideReasonCode = null;
        WeightOverrideReasonOther = null;
        IsWeightOverrideReasonPanelVisible = false;
        IsManualWeightOverrideMessageVisible = false;
        CanLoadTicketIntoForm = true;
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
        _draft.DeveloperWeightUnlockEnabled = IsDevWeightEditUnlocked;
        _draft.WeightOverrideReasonCode = WeightOverrideReasonCode;
        _draft.WeightOverrideReasonOther = WeightOverrideReasonOther;

        if (IsDevWeightEditUnlocked)
        {
            _draft.DraftWeight1 = ParseDevWeight(DevWeight1Text);
            _draft.DraftWeight2 = ParseDevWeight(DevWeight2Text);
        }
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
        SuppressAutocomplete = true;
        try
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
        finally
        {
            SuppressAutocomplete = false;
        }
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
        if (IsEditingExistingTicket)
        {
            IsWeigh1Enabled = false;
            IsWeigh2Enabled = false;
            return;
        }

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

    private static decimal? ParseDevWeight(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var kg)
            ? kg
            : null;
    }

    private void UpdateWeightOverrideUi()
    {
        var weightChanged = HasDevWeightChanged();
        IsWeightOverrideReasonPanelVisible = IsDevWeightEditUnlocked && weightChanged;
        IsManualWeightOverrideMessageVisible = IsDevWeightEditUnlocked &&
            (weightChanged || _draft.LoadedWeight1HadOverride || _draft.LoadedWeight2HadOverride);
    }

    private bool HasDevWeightChanged() =>
        !NullableWeightEquals(_draft.DraftWeight1, _draft.LoadedEffectiveWeight1Kg) ||
        !NullableWeightEquals(_draft.DraftWeight2, _draft.LoadedEffectiveWeight2Kg);

    private static bool NullableWeightEquals(decimal? a, decimal? b) =>
        a.HasValue == b.HasValue && (!a.HasValue || a.Value == b.Value);

    private async Task FlashHighlightTicketAsync(int ticketId, bool scrollToTop)
    {
        HighlightedTicketId = ticketId;
        SelectedTicket = Tickets.FirstOrDefault(t => t.Id == ticketId);
        _focusService.HighlightTicketRow(ticketId, scrollToTop);
        await Task.Delay(1500);
        HighlightedTicketId = null;
    }

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

public sealed record WeightOverrideReasonOption(string Code, string Label);
