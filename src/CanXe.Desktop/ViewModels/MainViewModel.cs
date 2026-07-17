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
using CanXe.Desktop.Controls;
using CanXe.Desktop.Services;
using CanXe.Infrastructure.Logging;
using CanXe.ScaleProtocol.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IHardwareScaleDiagnostics? _hardwareScale;
    private readonly StationSettingsService _stationSettings;
    private readonly WeighTicketService _weighTicketService;
    private readonly FastEntrySearchService _fastEntrySearch;
    private readonly IScaleService _scaleService;
    private readonly IUiFocusService _focusService;
    private readonly IWeighTicketPrintService _printService;
    private readonly IWeighTicketPrintWorkflow _printWorkflow;
    private readonly IWeighTicketDocumentFactory _documentFactory;
    private readonly PrintSettingsService _printSettings;
    private readonly PrintCommandLogger _printCommandLogger;
    private readonly IPrintNotificationService _printNotificationService;
    private readonly ITicketDeleteService _ticketDeleteService;
    private readonly IDeveloperAuthorizationService _developerAuthorization;
    private readonly ICatalogService _catalogService;
    private readonly AppSettings _settings;
    private readonly bool _developerModeEnabled;
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
    private int? _lastCompletedTicketId;
    private int? _lastPrintableTicketId;
    private CancellationTokenSource? _toastCts;
    private bool _unitPriceIsEditing;

    public MainViewModel(
        WeighTicketService weighTicketService,
        FastEntrySearchService fastEntrySearch,
        IScaleService scaleService,
        IHardwareScaleDiagnostics? hardwareScaleDiagnostics,
        StationSettingsService stationSettingsService,
        IUiFocusService focusService,
        IWeighTicketPrintService printService,
        IWeighTicketPrintWorkflow printWorkflow,
        IWeighTicketDocumentFactory documentFactory,
        PrintSettingsService printSettings,
        PrintCommandLogger printCommandLogger,
        IPrintNotificationService printNotificationService,
        ITicketDeleteService ticketDeleteService,
        IDeveloperAuthorizationService developerAuthorization,
        IAdminAuthorizationService adminAuthorization,
        IUserPermissionService userPermissionService,
        ICatalogService catalogService,
        AppSettings settings,
        SettingsViewModel settingsViewModel,
        DeveloperViewModel developerViewModel,
        CatalogViewModel catalogViewModel,
        ReportViewModel reportViewModel,
        AppPaths appPaths)
    {
        _weighTicketService = weighTicketService;
        _fastEntrySearch = fastEntrySearch;
        _scaleService = scaleService;
        _hardwareScale = hardwareScaleDiagnostics;
        _stationSettings = stationSettingsService;
        _focusService = focusService;
        _printService = printService;
        _printWorkflow = printWorkflow;
        _documentFactory = documentFactory;
        _printSettings = printSettings;
        _printCommandLogger = printCommandLogger;
        _printNotificationService = printNotificationService;
        _ticketDeleteService = ticketDeleteService;
        _developerAuthorization = developerAuthorization;
        _catalogService = catalogService;
        _settings = settings;
        _developerModeEnabled = settings.DeveloperMode;
        _appPaths = appPaths;
        Settings = settingsViewModel;
        Developer = developerViewModel;
        Catalog = catalogViewModel;
        Report = reportViewModel;
        Settings.StationSettingsSaved += (_, dto) => OnStationSettingsSaved(dto);
        _scaleService.WeightChanged += OnScaleWeightChanged;
        if (_hardwareScale is not null)
            _hardwareScale.HardwareDiagnosticsChanged += (_, _) =>
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateHardwareDiagnostics();
                    NotifyDevDiagnosticsBindings();
                });
        IsDeveloperPanelAvailable = settings.DeveloperMode && settings.ShowDeveloperTab;
        IsDeveloperWeightUnlockVisible = false;
        IsDeveloperDeleteVisible = false;
        WireAdminServices(adminAuthorization, userPermissionService);
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
    [ObservableProperty] private bool _isPrinting;
    [ObservableProperty] private bool _isContinuationMode;
    [ObservableProperty] private ScaleInputMode _scaleInputMode = ScaleInputMode.SimulationAutomatic;
    [ObservableProperty] private bool _suppressAutocomplete;
    [ObservableProperty] private bool _isCompactMode;
    [ObservableProperty] private AppNavigationSection _activeSection = AppNavigationSection.WeighTicket;
    [ObservableProperty] private string _simulatedWeightText = "18500";
    [ObservableProperty] private bool _simulateScaleStable = true;
    [ObservableProperty] private bool _simulateScaleDisconnected;
    [ObservableProperty] private bool _simulateScaleError;

    public SettingsViewModel Settings { get; }
    public DeveloperViewModel Developer { get; }
    public CatalogViewModel Catalog { get; }
    public ReportViewModel Report { get; }

    public bool IsWeighTicketSectionVisible => ActiveSection == AppNavigationSection.WeighTicket;
    public bool IsCatalogSectionVisible => ActiveSection == AppNavigationSection.Catalog;
    public bool IsDeviceSectionVisible => ActiveSection == AppNavigationSection.Device;
    public bool IsReportSectionVisible => ActiveSection == AppNavigationSection.Report;
    public bool IsSettingsSectionVisible => ActiveSection == AppNavigationSection.Settings;
    public bool IsSystemSectionVisible => ActiveSection == AppNavigationSection.System;
    public bool IsDeveloperSectionVisible => IsDeveloperPanelAvailable && ActiveSection == AppNavigationSection.Developer;

    partial void OnActiveSectionChanged(AppNavigationSection value)
    {
        OnPropertyChanged(nameof(IsWeighTicketSectionVisible));
        OnPropertyChanged(nameof(IsCatalogSectionVisible));
        OnPropertyChanged(nameof(IsDeviceSectionVisible));
        OnPropertyChanged(nameof(IsReportSectionVisible));
        OnPropertyChanged(nameof(IsSettingsSectionVisible));
        OnPropertyChanged(nameof(IsSystemSectionVisible));
        OnPropertyChanged(nameof(IsDeveloperSectionVisible));
        OnPropertyChanged(nameof(IsNavSystemActive));
        OnPropertyChanged(nameof(IsNavWeighTicketActive));
        OnPropertyChanged(nameof(IsNavCatalogActive));
        OnPropertyChanged(nameof(IsNavDeviceActive));
        OnPropertyChanged(nameof(IsNavReportActive));
        OnPropertyChanged(nameof(IsNavSettingsActive));
        OnPropertyChanged(nameof(IsNavDeveloperActive));
        if (value == AppNavigationSection.Catalog)
        {
            // Fire-and-forget; Catalog.InitializeAsync must never throw to dispatcher.
            _ = Catalog.InitializeAsync();
        }
        else if (value == AppNavigationSection.Report)
            _ = Report.InitializeAsync();
    }

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
    [ObservableProperty] private string _headerScaleStatus = "● Đầu cân: Đang kết nối";
    [ObservableProperty] private string _headerClockText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    [ObservableProperty] private string? _lastSavedTicketDisplayNumber;

    [ObservableProperty] private bool _isEditingExistingTicket;
    [ObservableProperty] private long? _editingTicketId;
    [ObservableProperty] private string? _editingTicketNumber;
    [ObservableProperty] private bool _isDeveloperWeightUnlockVisible;
    [ObservableProperty] private bool _isDeveloperDeleteVisible;
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
        new(WorkAreaLayoutCalculator.GetMainColumnStars(WindowWidth).WeighStars, GridUnitType.Star);

    public GridLength InfoColumnWidth =>
        new(WorkAreaLayoutCalculator.GetMainColumnStars(WindowWidth).InfoStars, GridUnitType.Star);

    public double LiveWeightFontSize =>
        OperatorLayoutMetrics.GetLiveWeightFontSize(WindowWidth, WindowHeight);

    public double LiveWeightUnitFontSize =>
        WorkAreaLayoutCalculator.GetLiveWeightUnitFontSize(WindowWidth);

    public double WeighPanelValueFontSize =>
        WorkAreaLayoutCalculator.GetWeighPanelValueFontSize(WindowWidth);

    public double SummaryValueFontSize =>
        WorkAreaLayoutCalculator.GetSummaryValueFontSize(WindowWidth);

    public bool IsReturnToAutomaticVisible => ScaleInputMode == ScaleInputMode.SimulationManual;

    public string WeightSourceText => ScaleInputModeDisplay.GetWeightSourceText(ScaleInputMode);

    public bool IsNavSystemActive => ActiveSection == AppNavigationSection.System;
    public bool IsNavWeighTicketActive => ActiveSection == AppNavigationSection.WeighTicket;
    public bool IsNavCatalogActive => ActiveSection == AppNavigationSection.Catalog;
    public bool IsNavDeviceActive => ActiveSection == AppNavigationSection.Device;
    public bool IsNavReportActive => ActiveSection == AppNavigationSection.Report;
    public bool IsNavSettingsActive => ActiveSection == AppNavigationSection.Settings;
    public bool IsNavDeveloperActive => ActiveSection == AppNavigationSection.Developer;

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

    public bool IsCreateModeActionsVisible =>
        FormMode is TicketFormMode.Browsing or TicketFormMode.Creating or TicketFormMode.AwaitingSecondWeigh;
    public bool IsEditModeActionsVisible => FormMode == TicketFormMode.Editing;
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
        ActiveSection = AppNavigationSection.WeighTicket;
        UpdateWindowSize(SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        if (IsCompactMode)
        {
            IsAdvancedFilterVisible = false;
        }

        await Settings.LoadAsync(_appPaths.DatabasePath);

        var effectiveDeviceMode = EffectiveDeviceMode;
        var savedFromDb = Settings.SavedScaleInputMode;
        if (ScaleInputModeDisplay.ShouldNormalizeLegacyScaleMode(effectiveDeviceMode, savedFromDb))
            ScaleModeLogger.WriteNormalization(savedFromDb, ScaleInputMode.Hardware, effectiveDeviceMode);

        var startupMode = ScaleInputModeDisplay.ResolveStartupMode(effectiveDeviceMode, savedFromDb);
        await ApplyScaleInputModeAsync(startupMode, userInitiated: false, persistSettings: false);

        if (Settings.SavedScaleInputMode != ScaleInputMode)
            await Settings.SaveScaleInputModeAsync(ScaleInputMode);

        LogScaleModeState("Startup");

        HeaderClockText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
        _ = RunClockAsync();
        await RefreshPreviewDisplayNumberAsync();
        LoadBindingsFromDraft();
        await FilterTodayAsync();

        _focusService.FocusCustomerField();
        _ = AutoConnectScaleIfNeededAsync();
        _ = ScheduleAutoBackupAsync();
    }

    private async Task ScheduleAutoBackupAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            await Settings.RunAutoBackupIfNeededAsync().ConfigureAwait(false);
        }
        catch
        {
            // Auto-backup must never surface to UI.
        }
    }

    [RelayCommand]
    private void Navigate(AppNavigationSection section) => ActiveSection = section;

    [RelayCommand]
    private void ToggleAdvancedFilter() => IsAdvancedFilterVisible = !IsAdvancedFilterVisible;

    private void NotifyWorkAreaLayoutChanged()
    {
        OnPropertyChanged(nameof(WeighColumnWidth));
        OnPropertyChanged(nameof(InfoColumnWidth));
        OnPropertyChanged(nameof(LiveWeightFontSize));
        OnPropertyChanged(nameof(LiveWeightUnitFontSize));
        OnPropertyChanged(nameof(WeighPanelValueFontSize));
        OnPropertyChanged(nameof(SummaryValueFontSize));
        OnPropertyChanged(nameof(WorkspaceMaxHeight));
        OnPropertyChanged(nameof(WorkspaceMinHeight));
        OnPropertyChanged(nameof(DataGridMinHeight));
        OnPropertyChanged(nameof(LiveWeightViewboxMaxHeight));
        OnPropertyChanged(nameof(FormFieldHeight));
        OnPropertyChanged(nameof(NotesFieldHeight));
        OnPropertyChanged(nameof(SummaryFooterMaxHeight));
        OnPropertyChanged(nameof(TicketGridRowHeight));
    }

    [RelayCommand]
    private async Task SelectSimulationAutomaticModeAsync() =>
        await ApplyScaleInputModeAsync(ScaleInputMode.SimulationAutomatic, userInitiated: true);

    [RelayCommand]
    private async Task SelectSimulationManualModeAsync() =>
        await ApplyScaleInputModeAsync(ScaleInputMode.SimulationManual, userInitiated: true);

    [RelayCommand]
    private async Task ReturnToAutomaticModeAsync() =>
        await ApplyScaleInputModeAsync(ScaleInputMode.SimulationAutomatic, userInitiated: true);

    [RelayCommand]
    private async Task SaveScaleConfigurationAsync()
    {
        var runtimeMode = ScaleInputMode;
        var persistMode = ScaleInputModeDisplay.GetPersistedScaleInputMode(EffectiveDeviceMode, runtimeMode);

        if (_hardwareScale?.IsConnected == true)
            await DisconnectHardwareAsync().ConfigureAwait(false);

        Settings.SavedScaleInputMode = persistMode;
        await Settings.PersistScaleSettingsAsync(persistMode).ConfigureAwait(false);

        var applyMode = ScaleInputModeDisplay.IsHardwareDeviceMode(EffectiveDeviceMode)
            ? ScaleInputMode.Hardware
            : runtimeMode;

        await ApplyScaleInputModeAsync(applyMode, userInitiated: false, persistSettings: false)
            .ConfigureAwait(false);

        if (applyMode == ScaleInputMode.Hardware && Settings.AutoConnectScaleOnStartup)
        {
            _disconnectedByUser = false;
            _ = AutoConnectScaleIfNeededAsync();
        }

        Settings.SettingsStatusMessage = "Đã lưu cấu hình đầu cân.";
        LogScaleModeState("SaveScaleConfiguration");
    }

    private async Task ApplyScaleInputModeAsync(
        ScaleInputMode mode,
        bool userInitiated,
        bool persistSettings = true)
    {
        if (!ScaleInputModeDisplay.IsValidModeForDevice(EffectiveDeviceMode, mode, _developerModeEnabled))
            return;

        if (mode != ScaleInputMode.Hardware)
            _autoConnectCts?.Cancel();

        if (mode == ScaleInputMode.Hardware)
        {
            _hasReceivedHardwareFrame = false;
            LiveWeightKg = 0;
            RefreshLiveWeightDisplay();
        }
        else if (ScaleInputMode == ScaleInputMode.Hardware)
        {
            _hasReceivedHardwareFrame = false;
            LiveWeightKg = 0;
        }

        await SetCompositeScaleInputModeAsync(mode).ConfigureAwait(false);
        ScaleInputMode = _hardwareScale?.InputMode ?? mode;
        Settings.SavedScaleInputMode = ScaleInputMode;

        switch (ScaleInputMode)
        {
            case ScaleInputMode.SimulationAutomatic:
                if (userInitiated)
                    StatusMessage = "Đã chuyển về chế độ tự động mô phỏng.";
                LiveWeightKg = await _scaleService.GetCurrentWeightAsync().ConfigureAwait(false);
                break;
            case ScaleInputMode.SimulationManual:
                if (userInitiated)
                    StatusMessage = "Đang dùng nguồn thủ công mô phỏng.";
                try
                {
                    LiveWeightKg = await _scaleService.GetCurrentWeightAsync().ConfigureAwait(false);
                }
                catch
                {
                    LiveWeightKg = 0;
                }
                break;
            case ScaleInputMode.Hardware:
                if (userInitiated)
                    StatusMessage = "Đang dùng nguồn đầu cân COM.";
                RefreshLiveWeightDisplay();
                if (userInitiated && Settings.AutoConnectScaleOnStartup && !_disconnectedByUser)
                    _ = AutoConnectScaleIfNeededAsync();
                break;
        }

        if (persistSettings && userInitiated && ScaleInputModeDisplay.ShouldPersistMode(EffectiveDeviceMode, ScaleInputMode))
            await Settings.SaveScaleInputModeAsync(ScaleInputMode).ConfigureAwait(false);

        IsHardwareModeSelected = ScaleInputMode == ScaleInputMode.Hardware;

        UpdateScaleStatusDisplay();
        UpdateHardwareDiagnostics();
        NotifyScaleInputModeBindings();
        NotifyDevDiagnosticsBindings();
        LogScaleModeState(userInitiated ? "UserModeChange" : "ApplyScaleInputMode");
    }

    private async Task SetCompositeScaleInputModeAsync(ScaleInputMode mode)
    {
        if (_hardwareScale is not null)
        {
            await _hardwareScale.SetInputModeAsync(mode).ConfigureAwait(false);
            return;
        }

        switch (mode)
        {
            case ScaleInputMode.SimulationAutomatic:
                await _scaleService.StartAsync().ConfigureAwait(false);
                _scaleService.ResumeAutomaticSimulation();
                _scaleService.SetManualMode(false);
                break;
            case ScaleInputMode.SimulationManual:
                await _scaleService.StartAsync().ConfigureAwait(false);
                _scaleService.SetManualMode(true);
                break;
            case ScaleInputMode.Hardware:
                await _scaleService.StopAsync().ConfigureAwait(false);
                break;
        }
    }

    private ScaleInputMode GetActiveServiceMode() =>
        _hardwareScale?.InputMode ?? ScaleInputMode;

    private void LogScaleModeState(string eventName)
    {
        var serviceMode = GetActiveServiceMode();
        var simulationRunning = serviceMode != ScaleInputMode.Hardware;
        ScaleModeLogger.Write(
            eventName,
            EffectiveDeviceMode,
            Settings.SavedScaleInputMode,
            ScaleInputMode,
            ScaleInputMode,
            serviceMode,
            simulationRunning,
            _hardwareScale?.IsConnected == true);
    }

    private ScaleHeaderConnectionState GetHardwareHeaderConnectionState()
    {
        if (IsScaleConnecting || _hardwareScale?.ConnectionState == ScaleConnectionState.Connecting)
            return ScaleHeaderConnectionState.Connecting;
        if (_hardwareScale is null || !_hardwareScale.IsConnected)
            return ScaleHeaderConnectionState.Disconnected;
        if (_hardwareScale.IsStale)
            return ScaleHeaderConnectionState.Disconnected;
        if (!_hasReceivedHardwareFrame || _hardwareScale.LatestReading is null)
            return ScaleHeaderConnectionState.WaitingForData;
        return ScaleHeaderConnectionState.Connected;
    }

    private void NotifyScaleInputModeBindings()
    {
        OnPropertyChanged(nameof(IsHardwareScaleMode));
        OnPropertyChanged(nameof(IsAutomaticSimulationMode));
        OnPropertyChanged(nameof(IsManualSimulationMode));
        OnPropertyChanged(nameof(IsSimulationAutomaticMode));
        OnPropertyChanged(nameof(IsSimulationManualMode));
        OnPropertyChanged(nameof(IsHardwareMode));
        OnPropertyChanged(nameof(IsSimulationScaleSourceSelectionEnabled));
        OnPropertyChanged(nameof(IsHardwareModeEnabled));
        OnPropertyChanged(nameof(IsHardwareOperationsPanelVisible));
        OnPropertyChanged(nameof(IsScaleSourceSelectionVisible));
        OnPropertyChanged(nameof(IsReturnToAutomaticVisible));
        OnPropertyChanged(nameof(IsManualWeightInputVisible));
        OnPropertyChanged(nameof(WeightSourceText));
    }

    private void UpdateScaleStatusDisplay()
    {
        if (ScaleInputMode == ScaleInputMode.Hardware && _hardwareScale is not null)
        {
            HeaderScaleStatus = ScaleInputModeDisplay.GetHeaderScaleBadge(
                ScaleInputMode,
                GetHardwareHeaderConnectionState());
            ScaleStabilityText = HardwareStableText;
            return;
        }

        HeaderScaleStatus = ScaleInputModeDisplay.GetHeaderScaleBadge(
            ScaleInputMode,
            SimulateScaleDisconnected);
        ScaleStabilityText = SimulateScaleDisconnected
            ? "● MẤT KẾT NỐI"
            : SimulateScaleStable ? "● ỔN ĐỊNH" : "● ĐANG THAY ĐỔI";
    }

    partial void OnIsScaleConnectingChanged(bool value)
    {
        UpdateScaleStatusDisplay();
        RefreshLiveWeightDisplay();
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(OperatorBarText));

    partial void OnOperatorStatusMessageChanged(string value) => OnPropertyChanged(nameof(OperatorBarText));

    partial void OnSimulateScaleStableChanged(bool value) => UpdateScaleStatusDisplay();
    partial void OnSimulateScaleDisconnectedChanged(bool value) => UpdateScaleStatusDisplay();
    partial void OnScaleInputModeChanged(ScaleInputMode value)
    {
        UpdateScaleStatusDisplay();
        NotifyScaleInputModeBindings();
    }

    [RelayCommand]
    private void ApplySimulatedWeight()
    {
        if (!ManualSimulationInputHelper.TryParseKg(SimulatedWeightText, out var kg, out var error))
        {
            ManualSimulationInputError = error;
            NotifyDevDiagnosticsBindings();
            StatusMessage = error ?? "Không thể áp dụng trọng lượng mô phỏng.";
            return;
        }

        ManualSimulationInputError = null;
        SimulatedWeightText = kg.ToString(CultureInfo.CurrentCulture);
        SetManualWeight(kg);
        StatusMessage = $"Đã áp dụng trọng lượng mô phỏng: {kg:N0} kg";
    }

    [RelayCommand]
    private void ResetSimulatedWeightToZero()
    {
        ManualSimulationInputError = null;
        SimulatedWeightText = "0";
        SetManualWeight(0);
        StatusMessage = "Đã đặt trọng lượng mô phỏng về 0 kg.";
    }

    [RelayCommand]
    private void ApplyPresetWeight8500() => SetManualWeight(8500);

    [RelayCommand]
    private void ApplyPresetWeight18500() => SetManualWeight(18500);

    private void OnStationSettingsSaved(StationSettingsDto dto)
    {
        StatusMessage = "Thông tin trạm cân đã cập nhật — preview phiếu dùng dữ liệu mới.";
    }

    private async Task RunClockAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            HeaderClockText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
        }
    }

    [ObservableProperty] private bool _isSaving;

    [RelayCommand(CanExecute = nameof(CanSaveTicket))]
    private async Task SaveAsync()
    {
        if (IsSaving)
            return;

        IsSaving = true;
        SaveCommand.NotifyCanExecuteChanged();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            SyncDraftFromBindings();
            StatusMessage = "ĐANG LƯU...";
            var wasContinuationSave = _draft.ExistingTicketId.HasValue;
            WeighWorkflowLogger.Write("SAVE_DRAFT_REQUESTED",
                BuildSaveDraftStateSummary());
            WeighWorkflowLogger.Write(
                _draft.ExistingTicketId.HasValue ? "SAVE_SECOND_WEIGH_STARTED" : "SAVE_FIRST_WEIGH_STARTED",
                $"TicketId={_draft.ExistingTicketId?.ToString() ?? "new"}");

            var learnCustomer = NullIfWhiteSpace(_draft.DraftCustomer);
            var learnPlate = NullIfWhiteSpace(_draft.DraftVehicle);
            var learnCargo = NullIfWhiteSpace(_draft.DraftCargoType);

            var filter = BuildCurrentFilter();
            var dbSw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _weighTicketService.SaveAsync(_draft, filter);
            dbSw.Stop();

            if (!result.Success)
            {
                WeighWorkflowLogger.Write("SAVE_DRAFT_VALIDATION_FAILED", result.ErrorMessage ?? "unknown");
                StatusMessage = result.ErrorMessage ?? "Lưu thất bại.";
                MessageBox.Show(
                    result.ErrorMessage ?? "Không thể lưu phiếu cân.",
                    "Lỗi lưu phiếu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                OperatorActionLogger.WritePerformance("SaveTicket", $"total={sw.ElapsedMilliseconds}ms db={dbSw.ElapsedMilliseconds}ms result=fail");
                return;
            }

            var displayNumber = result.SavedTicket!.DisplayNumber;
            var savedId = result.SavedTicket.Id;
            LastSavedTicketDisplayNumber = displayNumber;

            if (result.WorkflowState == WeighTicketWorkflowState.AwaitingSecondWeigh)
            {
                _isApplyingTicketToForm = true;
                SuppressAutocomplete = true;
                try
                {
                    _draft = await _weighTicketService.LoadTicketForContinuationAsync(savedId);
                    LoadBindingsFromDraft(skipAutocompleteRefresh: true);
                    FormMode = TicketFormMode.AwaitingSecondWeigh;
                    ActiveTicketId = savedId;
                    ViewingTicketNumber = displayNumber;
                    IsPreviewDisplayNumber = false;
                    CaptureFormSnapshot();
                    UpdateButtonStates();
                    UpdateButtonLabels();
                }
                finally
                {
                    SuppressAutocomplete = false;
                    _isApplyingTicketToForm = false;
                    _focusService.RefreshAutocompleteDisplays();
                }

                WeighWorkflowLogger.Write("SAVE_FIRST_WEIGH_COMPLETED", $"TicketId={savedId}");
                WeighWorkflowLogger.Write("SAVE_NEW_TICKET_WITH_WEIGHT1", $"TicketId={savedId}");
            }
            else
            {
                _lastCompletedTicketId = savedId;
                _lastPrintableTicketId = savedId;
                try
                {
                    _lastSavedTicketDetail = await _weighTicketService.GetTicketDetailAsync(savedId);
                }
                catch (Exception ex)
                {
                    WeighWorkflowLogger.Write("SAVE_DETAIL_LOAD_FAILED", ex.Message);
                    AppExceptionLogger.WriteError("SaveAsync.GetTicketDetailAsync", ex, BuildSaveDraftStateSummary());
                }

                WeighWorkflowLogger.Write("SAVE_SECOND_WEIGH_COMPLETED", $"TicketId={savedId}");
                if (_draft.HasCapturedWeight1 && _draft.HasCapturedWeight2 && !_draft.ExistingTicketId.HasValue)
                    WeighWorkflowLogger.Write("SAVE_NEW_TICKET_WITH_WEIGHT1_WEIGHT2", $"TicketId={savedId}");
                WeighWorkflowLogger.Write("SAVE_FULL_DRAFT_COMPLETED", $"TicketId={savedId}");
                WeighWorkflowLogger.Write("PRINT_TARGET_SET_LAST_COMPLETED", $"TicketId={savedId}");
                await ResetToNewDraftAsync("save-completed");
                WeighWorkflowLogger.Write("FORM_RESET_AFTER_COMPLETED_SAVE", $"AfterTicketId={savedId}");
                WeighWorkflowLogger.Write("FORM_RESET_FOR_NEW_TICKET", $"AfterTicketId={savedId}");
                if (wasContinuationSave)
                    WeighWorkflowLogger.Write("SAVE_CONTINUATION_PRESERVE_METADATA", $"TicketId={savedId}");
            }

            if (result.SavedTicket is not null)
            {
                try
                {
                    UpsertSavedTicketInList(
                        result.SavedTicket,
                        TicketListCollectionPolicy.TreatSavedTicketAsNewListEntry(wasContinuationSave));
                    WeighWorkflowLogger.Write("SAVE_COLLECTION_UPSERTED",
                        $"TicketId={savedId} NewEntry={!wasContinuationSave}");
                    await RefreshSummaryOnlyAsync();
                }
                catch (Exception ex)
                {
                    WeighWorkflowLogger.Write("SAVE_COLLECTION_INSERT_FAILED", ex.Message);
                    AppExceptionLogger.WriteError("SaveAsync.CollectionUpdate", ex, BuildSaveDraftStateSummary());
                }
            }
            else
            {
                await RefreshSummaryOnlyAsync();
            }

            try
            {
                await _catalogService.LearnFromTicketAsync(learnCustomer, learnPlate, learnCargo);
            }
            catch (Exception ex)
            {
                WeighWorkflowLogger.Write("CATALOG_AUTO_LEARN_FAILED", ex.Message);
                AppExceptionLogger.WriteError("SaveAsync.AutoLearn", ex, BuildSaveDraftStateSummary());
            }

            OperatorActionLogger.WritePerformance("SaveTicket",
                $"total={sw.ElapsedMilliseconds}ms db={dbSw.ElapsedMilliseconds}ms historyRefresh=inline statsRefresh=incremental");

            if (result.SimilarCustomerWarnings.Count > 0)
                StatusMessage = string.Join(" ", result.SimilarCustomerWarnings);
            else if (result.WorkflowState == WeighTicketWorkflowState.AwaitingSecondWeigh)
                StatusMessage = $"Đã lưu phiếu {displayNumber}. CHỜ CÂN LẦN 2 — bấm LẤY CÂN LẦN 2.";
            else
                StatusMessage = $"Đã hoàn tất phiếu {displayNumber}. Sẵn sàng cân xe mới.";

            if (!result.IsVisibleInCurrentFilter)
                StatusMessage += " (phiếu không khớp bộ lọc danh sách hiện tại)";

            try
            {
                await FlashHighlightTicketAsync(savedId, scrollToTop: true);
                _focusService.FocusCustomerField();
            }
            catch (Exception ex)
            {
                WeighWorkflowLogger.Write("SAVE_POST_UI_FAILED", ex.Message);
                AppExceptionLogger.WriteError("SaveAsync.PostSaveUi", ex, BuildSaveDraftStateSummary());
            }
        }
        catch (Exception ex)
        {
            WeighWorkflowLogger.Write("SAVE_EXCEPTION", ex.GetType().Name);
            AppExceptionLogger.WriteError("SaveAsync", ex, BuildSaveDraftStateSummary());
            AppExceptionLogger.MarkSaveErrorHandled();
            StatusMessage = "Không thể lưu phiếu cân.";
            MessageBox.Show(
                "KHÔNG THỂ LƯU PHIẾU\nĐã xảy ra lỗi khi lưu phiếu cân. Vui lòng thử lại hoặc gửi log cho kỹ thuật.",
                "Lỗi lưu phiếu",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            OperatorActionLogger.WritePerformance("SaveTicket", $"total={sw.ElapsedMilliseconds}ms result=exception");
        }
        finally
        {
            IsSaving = false;
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSaveTicket() => !IsSaving;

    private static string BuildSaveDraftStateSummary(WeighTicketDraft? draft = null) =>
        draft is null
            ? "draft=null"
            : $"TicketId={draft.ExistingTicketId?.ToString() ?? "new"} Weight1Captured={draft.HasCapturedWeight1} Weight2Captured={draft.HasCapturedWeight2} Customer={(string.IsNullOrWhiteSpace(draft.DraftCustomer) ? "empty" : "set")} Plate={(string.IsNullOrWhiteSpace(draft.DraftVehicle) ? "empty" : "set")} Cargo={(string.IsNullOrWhiteSpace(draft.DraftCargoType) ? "empty" : "set")} UnitPrice={(draft.DraftUnitPrice.HasValue ? "set" : "empty")}";

    private string BuildSaveDraftStateSummary() => BuildSaveDraftStateSummary(_draft);

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task RefreshSummaryOnlyAsync()
    {
        var filter = BuildCurrentFilter();
        var result = await _weighTicketService.GetFilteredWithSummaryAsync(filter);
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

    private void UpsertSavedTicketInList(WeighTicketListItem savedTicket, bool treatAsNewListEntry)
    {
        var index = -1;
        for (var i = 0; i < Tickets.Count; i++)
        {
            if (Tickets[i].Id == savedTicket.Id)
            {
                index = i;
                break;
            }
        }

        if (treatAsNewListEntry)
        {
            if (index >= 0)
                Tickets.RemoveAt(index);
            Tickets.Insert(0, savedTicket);
            return;
        }

        if (index >= 0)
            Tickets[index] = savedTicket;
        else
            InsertTicketAtSortedPosition(savedTicket);
    }

    private void InsertTicketAtSortedPosition(WeighTicketListItem savedTicket)
    {
        var insertIndex = TicketListCollectionPolicy.FindInsertIndex(
            Tickets,
            savedTicket,
            item => item.TicketDateTime,
            item => item.Id);
        Tickets.Insert(insertIndex, savedTicket);
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsFormDirty())
        {
            var confirm = MessageBox.Show(
                "Dữ liệu đang nhập chưa lưu. Bạn có muốn hủy bỏ không?",
                "Xác nhận hủy",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
        }

        _weighTicketService.CancelDraft(_draft);
        await ResetToNewDraftAsync("cancel");
        StatusMessage = "Đã hủy nhập liệu.";
        _focusService.FocusCustomerField();
    }

    [RelayCommand(CanExecute = nameof(CanPrintTicket))]
    private Task PrintTicketAsync() => ExecuteMainPrintAsync();

    public void RecordMainPrintButtonClick(MainPrintButtonTelemetry telemetry)
    {
        _printCommandLogger.Log(
            "MAIN_PRINT_BUTTON_CLICK_RECEIVED " +
            $"button={telemetry.ButtonName} " +
            $"isEnabled={telemetry.IsEnabled} " +
            $"isHitTestVisible={telemetry.IsHitTestVisible} " +
            $"dataContext={telemetry.DataContextType} " +
            $"command={telemetry.CommandType} " +
            $"canExecute={telemetry.CanExecute} " +
            $"activeTicketId={telemetry.ActiveTicketId?.ToString() ?? "null"} " +
            $"selectedTicketId={telemetry.SelectedTicketId?.ToString() ?? "null"} " +
            $"formMode={telemetry.FormMode} " +
            $"isDirty={telemetry.IsDirty} " +
            $"isPrinting={telemetry.IsPrinting}");
    }

    public async Task ExecuteMainPrintAsync()
    {
        _printCommandLogger.LogMilestone("MAIN_PRINT_COMMAND_EXECUTED");

        if (IsPrinting)
        {
            _printCommandLogger.LogMilestone("PRINT_CLICK_IGNORED_ALREADY_PRINTING");
            return;
        }

        var resolved = ResolvePrintTicketForExecution();
        if (resolved is null)
        {
            const string message = "Vui lòng chọn hoặc lưu phiếu trước khi in.";
            StatusMessage = message;
            _printNotificationService.ShowPrintError(message);
            return;
        }

        var ticketId = resolved.Value.TicketId;
        if (resolved.Value.IsDirty)
        {
            var choice = _printNotificationService.PromptDirtyPrintChoice();
            if (choice == PrintDirtyChoice.Cancel)
                return;
            if (choice == PrintDirtyChoice.PrintCurrent)
            {
                const string message = "Vui lòng cập nhật phiếu trước khi in.";
                StatusMessage = message;
                _printNotificationService.ShowPrintError(message);
                return;
            }
        }

        IsPrinting = true;
        PrintTicketCommand.NotifyCanExecuteChanged();
        StatusMessage = "ĐANG CHUẨN BỊ IN...";

        try
        {
            var result = await _printWorkflow.PrintTicketAsync(ticketId, PrintCommandSource.MainWindow).ConfigureAwait(true);
            StatusMessage = result.Success
                ? "ĐÃ GỬI ĐẾN MÁY IN"
                : result.ErrorMessage ?? "In thất bại";
        }
        catch (Exception ex)
        {
            _printCommandLogger.LogException("MAIN_PRINT_COMMAND_EXECUTED", ex);
            _printNotificationService.ShowPrintError(ex.Message);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsPrinting = false;
            PrintTicketCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrintTicket))]
    private async Task ViewTicketAsync()
    {
        var ticketId = ResolvePrintTicketId();
        if (ticketId is null)
        {
            StatusMessage = "Vui lòng chọn hoặc lưu phiếu trước khi xem.";
            return;
        }

        try
        {
            StatusMessage = "ĐANG CHUẨN BỊ PHIẾU...";
            await _printWorkflow.PreviewTicketAsync(ticketId.Value).ConfigureAwait(true);
            StatusMessage = "Đã đóng xem trước phiếu.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private bool CanPrintTicket() => !IsPrinting && ResolvePrintTicketCore() is not null;

    public bool IsTicketDirty => IsFormDirty();

    private readonly record struct ResolvedPrintTicket(int TicketId, string Source, bool IsDirty);

    private ResolvedPrintTicket? ResolvePrintTicketCore()
    {
        var dirty = IsFormDirty();

        if (FormMode == TicketFormMode.Editing && EditingTicketId is long editId)
            return new ResolvedPrintTicket((int)editId, "ActiveTicket", dirty);

        if (ActiveTicketId is int activeId &&
            FormMode is TicketFormMode.Viewing or TicketFormMode.AwaitingSecondWeigh)
            return new ResolvedPrintTicket(activeId, "ActiveTicket", dirty);

        if (FormMode == TicketFormMode.Creating && _lastCompletedTicketId is int completedId)
            return new ResolvedPrintTicket(completedId, "LastCompletedTicket", false);

        if (FormMode == TicketFormMode.Creating && _lastSavedTicketDetail is not null)
            return new ResolvedPrintTicket(_lastSavedTicketDetail.Id, "LastSavedTicket", false);

        return null;
    }

    private ResolvedPrintTicket? ResolvePrintTicketForExecution()
    {
        var resolved = ResolvePrintTicketCore();
        if (resolved is null)
        {
            _printCommandLogger.Log("PrintTargetSource=None PrintTargetId=null");
            return null;
        }

        _printCommandLogger.Log(
            $"PrintTargetSource={resolved.Value.Source} PrintTargetId={resolved.Value.TicketId}");
        return resolved;
    }

    private int? ResolvePrintTicketId() => ResolvePrintTicketCore()?.TicketId;

    partial void OnIsPrintingChanged(bool value)
    {
        PrintTicketCommand.NotifyCanExecuteChanged();
        ViewTicketCommand.NotifyCanExecuteChanged();
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

        try
        {
            _selectionLoadCts?.Cancel();
            _isApplyingTicketToForm = true;
            SuppressAutocomplete = true;
            _draft = await _weighTicketService.LoadTicketForEditAsync(item.Id);
            FormMode = TicketFormMode.Editing;
            IsEditingExistingTicket = true;
            EditingTicketId = item.Id;
            EditingTicketNumber = item.DisplayNumber;
            ActiveTicketId = item.Id;
            ViewingTicketNumber = item.DisplayNumber;
            IsDevWeightEditUnlocked = false;
            WeightOverrideReasonCode = null;
            WeightOverrideReasonOther = null;
            DevWeight1Text = _draft.DraftWeight1?.ToString("N0", CultureInfo.CurrentCulture);
            DevWeight2Text = _draft.DraftWeight2?.ToString("N0", CultureInfo.CurrentCulture);
            LoadBindingsFromDraft(skipAutocompleteRefresh: true);
            CaptureFormSnapshot();
            UpdateWeightOverrideUi();
            UpdateButtonStates();
            UpdateButtonLabels();
            StatusMessage = $"Đang chỉnh sửa phiếu {item.DisplayNumber}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            SuppressAutocomplete = false;
            _isApplyingTicketToForm = false;
            _focusService.RefreshAutocompleteDisplays();
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
        if (IsDevWeightEditUnlocked || string.Equals(WeightOverrideReasonCode, WeightOverrideReasons.AdminInline, StringComparison.Ordinal))
        {
            AdminAuditLogger.Write(
                "ADMIN_WEIGHT_EDIT",
                "SAVED",
                "Admin",
                ticketId: updatedId.ToString(),
                note: "completed-ticket-update");
        }

        await ResetToNewDraftAsync("update-completed");
        await RefreshListAsync();
        await FlashHighlightTicketAsync(updatedId, scrollToTop: false);
        // Keep last updated ticket as print target (Phase 5 rc10 workflow).
        _lastCompletedTicketId = updatedId;
        StatusMessage = $"Đã cập nhật phiếu {displayNumber}.";
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private async Task ExitEditAsync()
    {
        _weighTicketService.CancelDraft(_draft);
        await ResetToNewDraftAsync("exit-edit");
        StatusMessage = "Đã thoát chế độ chỉnh sửa.";
        _focusService.FocusCustomerField();
    }

    [RelayCommand]
    private void UnlockDevWeightEdit()
    {
        if (!EnsureAdminOrNotify(AdminPermission.CanEditCompletedTicket, "UNLOCK_WEIGHT_EDIT"))
            return;

        if (!IsEditingExistingTicket)
            return;

        IsDevWeightEditUnlocked = true;
        _draft.DeveloperWeightUnlockEnabled = true;
        DevWeight1Text = _draft.DraftWeight1?.ToString("N0", CultureInfo.CurrentCulture);
        DevWeight2Text = _draft.DraftWeight2?.ToString("N0", CultureInfo.CurrentCulture);
        UpdateWeightOverrideUi();
        StatusMessage = "Admin: đã mở khóa sửa trọng lượng.";
        _adminAuthorization.TouchAdminActivity();
    }

    [RelayCommand]
    public async Task ContinueTicketAsync(WeighTicketListItem? item) =>
        await OpenTicketFromListAsync(item);

    [RelayCommand]
    private async Task OpenTicketDetailAsync(WeighTicketListItem? item) =>
        await OpenTicketFromListAsync(item);

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
        if (SuppressAutocomplete)
            return;
        _ = DebouncedSearchAsync(() => _customerSearchCts, cts => _customerSearchCts = cts, SearchCustomersAsync);
    }

    partial void OnCargoTypeNameChanged(string? value)
    {
        if (!_suppressAutoFillEditTracking && !string.IsNullOrWhiteSpace(_lastAutoFilledPlate))
            _cargoEditedAfterAutoFill = true;
        if (SuppressAutocomplete)
            return;
        _ = DebouncedSearchAsync(() => _cargoSearchCts, cts => _cargoSearchCts = cts, SearchCargoTypesAsync);
    }

    partial void OnNotesChanged(string? value)
    {
        if (SuppressAutocomplete)
            return;
        _ = DebouncedSearchAsync(
            () => _notesSearchCts, cts => _notesSearchCts = cts, SearchNotesAsync);
    }

    partial void OnLicensePlateChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value) && value != value.ToUpperInvariant())
        {
            LicensePlate = value.ToUpperInvariant();
            return;
        }

        if (SuppressAutocomplete)
            return;
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
        if (_unitPriceIsEditing)
            _draft.DraftUnitPrice = UnitPriceInputHelper.Parse(value);
        UpdateDisplaysFromDraft();
    }

    public void BeginUnitPriceEdit() => _unitPriceIsEditing = true;

    public void CommitUnitPriceEdit()
    {
        _unitPriceIsEditing = false;
        UnitPriceText = UnitPriceInputHelper.CommitDisplay(UnitPriceText);
        _draft.DraftUnitPrice = UnitPriceInputHelper.Parse(UnitPriceText);
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
                _ = ApplyCargoDefaultUnitPriceIfEmptyAsync(text);
                _focusService.FocusUnitPriceField();
                break;
            case AutocompleteField.Notes:
                if (suggestion is not null && !suggestion.IsNewEntryOption)
                    Notes = text;
                break;
        }
    }

    public async Task ShowFieldHistoryAsync(AutocompleteField field, AutoCompleteTextBox box)
    {
        try
        {
            WeighWorkflowLogger.Write("INPUT_HISTORY_REQUESTED", $"field={field}");

            IReadOnlyList<AutocompleteSuggestionItem> items = field switch
            {
                AutocompleteField.Customer => await _fastEntrySearch.GetCustomerHistoryAsync(),
                AutocompleteField.Vehicle => await _fastEntrySearch.GetVehicleHistoryAsync(),
                AutocompleteField.CargoType => await _fastEntrySearch.GetCargoTypeHistoryAsync(),
                _ => Array.Empty<AutocompleteSuggestionItem>()
            };

            ObservableCollection<AutocompleteSuggestionItem>? target = field switch
            {
                AutocompleteField.Customer => CustomerSuggestions,
                AutocompleteField.Vehicle => VehicleSuggestions,
                AutocompleteField.CargoType => CargoTypeSuggestions,
                AutocompleteField.Notes => NotesSuggestions,
                _ => null
            };

            if (target is null)
                return;

            target.Clear();
            foreach (var item in items)
                target.Add(item);

            if (items.Count == 0)
            {
                WeighWorkflowLogger.Write("INPUT_HISTORY_EMPTY", $"field={field}");
                return;
            }

            box.OpenHistoryDropDown();
            WeighWorkflowLogger.Write("INPUT_HISTORY_OPENED", $"field={field} count={items.Count}");
        }
        catch (Exception ex)
        {
            WeighWorkflowLogger.Write("INPUT_HISTORY_FAILED", $"field={field} type={ex.GetType().Name}");
            AppExceptionLogger.WriteError($"ShowFieldHistoryAsync.{field}", ex);
        }
    }

    private async Task ApplyCargoDefaultUnitPriceIfEmptyAsync(string cargoName)
    {
        if (!string.IsNullOrWhiteSpace(UnitPriceText))
            return;

        var defaultPrice = await _fastEntrySearch.GetCargoDefaultUnitPriceAsync(cargoName);
        if (defaultPrice is not decimal price || price <= 0)
            return;

        UnitPriceText = UnitPriceInputHelper.FormatDisplay(price);
        _draft.DraftUnitPrice = price;
        UpdateDisplaysFromDraft();
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
            }

            if (applied.CargoTypeName is not null &&
                (! _cargoEditedAfterAutoFill || normalizedPlate != _lastAutoFilledPlate))
            {
                CargoTypeName = applied.CargoTypeName;
                _draft.DraftCargoTypeId = applied.CargoTypeId;
                _draft.DraftCargoType = applied.CargoTypeName;
            }
        }
        finally
        {
            _suppressAutoFillEditTracking = false;
            SuppressAutocomplete = false;
        }

        _lastAutoFilledPlate = normalizedPlate;
        // Auto-fill is silent — no toast/popup.
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
        catch (Exception ex)
        {
            WeighWorkflowLogger.Write("DEBOUNCED_SEARCH_FAILED", ex.GetType().Name);
            AppExceptionLogger.WriteError("DebouncedSearchAsync", ex);
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
            _ = ApplyScaleInputModeAsync(ScaleInputMode.SimulationManual, userInitiated: true);

        ManualWeightText = kg.ToString("N0", CultureInfo.CurrentCulture);
        _scaleService.SetManualWeightKg(kg);
    }

    private void OnScaleWeightChanged(object? sender, decimal weightKg)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (ScaleInputMode == ScaleInputMode.Hardware)
            {
                if (_hardwareScale is null || !_hardwareScale.IsConnected || _hardwareScale.IsStale)
                    return;

                _hasReceivedHardwareFrame = true;
            }

            LiveWeightKg = weightKg;
            RefreshLiveWeightDisplay();
        });
    }

    private async Task RefreshListAsync()
    {
        var selectedId = SelectedTicket?.Id;
        var filter = BuildCurrentFilter();
        var result = await _weighTicketService.GetFilteredWithSummaryAsync(filter);

        Tickets.Clear();
        foreach (var item in result.Items)
            Tickets.Add(item);

        if (selectedId.HasValue)
            SelectedTicket = Tickets.FirstOrDefault(t => t.Id == selectedId.Value);

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

    private async Task RefreshFooterSummaryAsync()
    {
        var filter = BuildCurrentFilter();
        var result = await _weighTicketService.GetFilteredWithSummaryAsync(filter);
        FilterResultSummary = ActiveFilterChips.Count > 0
            ? $"Đang hiển thị: {result.Count} phiếu"
            : string.Empty;
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

    private async Task ResetToNewDraftAsync(string reason)
    {
        WeighWorkflowLogger.Write("RESET_TO_NEW_DRAFT", $"reason={reason}");
        _isResettingDraft = true;
        SuppressAutocomplete = true;
        _selectionLoadCts?.Cancel();
        try
        {
            _draft = new WeighTicketDraft();
            DeveloperWeight1OverrideEnabled = false;
            FormMode = TicketFormMode.Creating;
            ActiveTicketId = null;
            ViewingTicketNumber = null;
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
            CustomerName = null;
            LicensePlate = null;
            CargoTypeName = null;
            UnitPriceText = null;
            Notes = null;
            CustomerSuggestions.Clear();
            VehicleSuggestions.Clear();
            CargoTypeSuggestions.Clear();
            NotesSuggestions.Clear();
            ClearFormSnapshot();
            LoadBindingsFromDraft(skipAutocompleteRefresh: true);
            await RefreshPreviewDisplayNumberAsync();
            UpdateButtonStates();
            UpdateButtonLabels();
        }
        finally
        {
            SuppressAutocomplete = false;
            _isResettingDraft = false;
            _focusService.RefreshAutocompleteDisplays();
            WeighWorkflowLogger.Write("RESET_TO_NEW_DRAFT_COMPLETED", $"ticketNumber={DisplayNumber}");
        }
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
        if (_isApplyingTicketToForm || _isResettingDraft)
            return;

        _draft.DraftCustomer = CustomerName;
        _draft.DraftVehicle = LicensePlate;
        _draft.DraftCargoType = CargoTypeName;
        _draft.DraftNotes = Notes;
        _draft.DraftUnitPrice = UnitPriceInputHelper.Parse(UnitPriceText);
        _draft.DeveloperWeight1OverrideEnabled = DeveloperWeight1OverrideEnabled;
        _draft.DeveloperWeightUnlockEnabled = IsDevWeightEditUnlocked;
        _draft.WeightOverrideReasonCode = WeightOverrideReasonCode;
        _draft.WeightOverrideReasonOther = WeightOverrideReasonOther;

        if (IsDevWeightEditUnlocked)
        {
            var dev1 = ParseDevWeight(DevWeight1Text);
            var dev2 = ParseDevWeight(DevWeight2Text);
            if (dev1.HasValue)
                _draft.DraftWeight1 = dev1;
            if (dev2.HasValue)
                _draft.DraftWeight2 = dev2;
        }
    }

    private static decimal? ParseUnitPrice(string? text) => UnitPriceInputHelper.Parse(text);

    private void LoadBindingsFromDraft(bool skipAutocompleteRefresh = false)
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
            UnitPriceText = UnitPriceInputHelper.FormatDisplay(_draft.DraftUnitPrice);
            Notes = _draft.DraftNotes;
            DeveloperWeight1OverrideEnabled = _draft.DeveloperWeight1OverrideEnabled;
            UpdateDisplaysFromDraft();
            UpdateWeightSourceDisplays();
        }
        finally
        {
            SuppressAutocomplete = false;
            if (!skipAutocompleteRefresh)
                _focusService.RefreshAutocompleteDisplays();
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
        if (FormMode == TicketFormMode.Viewing)
        {
            IsWeigh1Enabled = false;
            IsWeigh2Enabled = false;
            return;
        }

        if (FormMode == TicketFormMode.Editing)
        {
            IsWeigh1Enabled = false;
            IsWeigh2Enabled = false;
            return;
        }

        _draft.DeveloperWeight1OverrideEnabled = DeveloperWeight1OverrideEnabled;
        IsWeigh1Enabled = !IsTakingWeight && DraftWorkflowRules.CanUpdateWeight1(
            _draft.IsWeight1LockedFromSavedTicket,
            _draft.DraftWeight2.HasValue,
            DeveloperWeight1OverrideEnabled);
        IsWeigh2Enabled = !IsTakingWeight && DraftWorkflowRules.CanUpdateWeight2(_draft.IsWeight2LockedFromSavedTicket);
    }

    private void UpdateButtonLabels()
    {
        if (IsTakingWeight && ActiveTakeWeightAction == TakeWeightAction.First)
            Weigh1ButtonText = TakeWeightStatusText ?? "ĐANG LẤY CÂN...";
        else if (_draft.IsWeight1LockedFromSavedTicket)
            Weigh1ButtonText = "CÂN LẦN 1 (ĐÃ LƯU)";
        else if (!IsWeigh1Enabled && _draft.DraftWeight2.HasValue)
            Weigh1ButtonText = "CÂN LẦN 1 ĐÃ KHÓA";
        else
            Weigh1ButtonText = _draft.DraftWeight1.HasValue ? "CẬP NHẬT CÂN LẦN 1" : "LẤY CÂN LẦN 1";

        if (IsTakingWeight && ActiveTakeWeightAction == TakeWeightAction.Second)
            Weigh2ButtonText = TakeWeightStatusText ?? "ĐANG LẤY CÂN...";
        else
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
        _autoConnectCts?.Cancel();
        _scaleService.WeightChanged -= OnScaleWeightChanged;
        if (_scaleService is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
        _takeWeightGate.Dispose();
        _vmConnectGate.Dispose();
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
