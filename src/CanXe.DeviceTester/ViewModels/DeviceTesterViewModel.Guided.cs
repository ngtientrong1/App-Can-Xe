using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CanXe.DeviceTester.Core.Guided;
using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.ViewModels;

public enum GuidedCapturePhase
{
    Idle,
    Instructions,
    EnterWeight,
    OpenPort,
    WaitForData,
    Stabilizing,
    Recording,
    TransitionEmpty,
    TransitionWaitStepOn,
    TransitionWaitStable,
    TransitionStableHold,
    TransitionWaitStepOff,
    TransitionWaitEmpty,
    TransitionFinalEmpty,
    ReviewQuality,
    SavePrompt,
    Completed
}

public partial class DeviceTesterViewModel
{
    private DeviceTesterCaptureMode _captureMode = DeviceTesterCaptureMode.Manual;
    private ComTestPreset _selectedPreset = ComTestPresets.Default;
    private int _guidedStepIndex;
    private GuidedCapturePhase _guidedPhase = GuidedCapturePhase.Idle;
    private int _countdownSeconds;
    private string _guidedInstruction = string.Empty;
    private string _guidedStepTitle = string.Empty;
    private string _qualityReport = string.Empty;
    private string _comparisonReport = string.Empty;
    private string _guidedConfigSummary = string.Empty;
    private bool _guidedSessionActive;
    private bool _guidedAwaitingSaveConfirm;
    private decimal? _guidedKnownWeightKg;
    private CaptureQualityResult? _lastQuality;
    private CaptureSaveResult? _pendingSaveResult;
    private CaptureRecordingStats? _pendingStats;
    private readonly List<GuidedSavedSession> _guidedSavedSessions = [];
    private readonly System.Windows.Threading.DispatcherTimer _guidedTimer = new();
    private GuidedCapturePhase _postCountdownPhase = GuidedCapturePhase.Recording;
    partial void InitializeGuidedCapture()
    {
        SetManualModeCommand = new RelayCommand(() => CaptureMode = DeviceTesterCaptureMode.Manual, () => IsModeSelectionEnabled);
        SetGuidedModeCommand = new RelayCommand(() => CaptureMode = DeviceTesterCaptureMode.Guided, () => IsModeSelectionEnabled);
        GuidedStartStepCommand = new RelayCommand(async () => await StartGuidedStepAsync(), () => CanStartGuidedStep());
        GuidedCancelSessionCommand = new RelayCommand(CancelGuidedSession, () => _guidedSessionActive && !IsRecording);
        GuidedSaveSessionCommand = new RelayCommand(async () => await SaveGuidedSessionAsync(), () => _guidedAwaitingSaveConfirm);
        GuidedRetrySessionCommand = new RelayCommand(RetryGuidedSession, () => _guidedSessionActive || _guidedAwaitingSaveConfirm);
        GuidedNextStepCommand = new RelayCommand(AdvanceGuidedPlan, () => CanAdvanceGuidedPlan());
        GuidedTransitionMarkerCommand = new RelayCommand(async () => await HandleTransitionMarkerAsync(), () => CanPressTransitionMarker());
        GuidedEmergencyStopCommand = new RelayCommand(EmergencyStopGuided, () => IsRecording);

        _guidedTimer.Interval = TimeSpan.FromSeconds(1);
        _guidedTimer.Tick += (_, _) => OnGuidedTimerTick();
        UpdateGuidedUi();
    }

    public IReadOnlyList<ComTestPreset> AvailableComTestPresets => ComTestPresets.All;
    public IReadOnlyList<CaptureEventMarker> GuidedEventMarkers => _capture.EventMarkers;

    public DeviceTesterCaptureMode CaptureMode
    {
        get => _captureMode;
        set
        {
            if (!SetField(ref _captureMode, value))
                return;
            OnPropertyChanged(nameof(IsManualMode));
            OnPropertyChanged(nameof(IsGuidedMode));
            OnPropertyChanged(nameof(IsModeSelectionEnabled));
            UpdateGuidedUi();
        }
    }

    public bool IsManualMode => CaptureMode == DeviceTesterCaptureMode.Manual;
    public bool IsGuidedMode => CaptureMode == DeviceTesterCaptureMode.Guided;
    public bool IsModeSelectionEnabled => !IsPortOpen && !IsRecording && !_guidedSessionActive;
    public bool IsPresetSelectionEnabled => IsModeSelectionEnabled && !IsRecording && !SelectedPreset.IsCustom;

    public ComTestPreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!SetField(ref _selectedPreset, value))
                return;
            if (!value.IsCustom)
                value.ApplyTo(BuildSettingsRef());
            OnPropertyChanged(nameof(BaudRate));
            OnPropertyChanged(nameof(DataBits));
            OnPropertyChanged(nameof(Parity));
            OnPropertyChanged(nameof(StopBits));
            OnPropertyChanged(nameof(Handshake));
            OnPropertyChanged(nameof(IsPresetSelectionEnabled));
        }
    }

    public string GuidedStepTitle { get => _guidedStepTitle; private set => SetField(ref _guidedStepTitle, value); }
    public string GuidedInstruction { get => _guidedInstruction; private set => SetField(ref _guidedInstruction, value); }
    public int CountdownSeconds { get => _countdownSeconds; private set => SetField(ref _countdownSeconds, value); }
    public string QualityReport { get => _qualityReport; private set => SetField(ref _qualityReport, value); }
    public string ComparisonReport { get => _comparisonReport; private set => SetField(ref _comparisonReport, value); }
    public string GuidedConfigSummary { get => _guidedConfigSummary; private set => SetField(ref _guidedConfigSummary, value); }
    public bool GuidedSessionActive => _guidedSessionActive;
    public bool GuidedAwaitingSave => _guidedAwaitingSaveConfirm;
    public bool IsGuidedBusy => _guidedSessionActive || IsRecording || _guidedAwaitingSaveConfirm;

    public decimal? GuidedKnownWeightKg
    {
        get => _guidedKnownWeightKg;
        set => SetField(ref _guidedKnownWeightKg, value);
    }

    public string GuidedPrimaryButtonText => _guidedPhase switch
    {
        GuidedCapturePhase.Instructions or GuidedCapturePhase.EnterWeight or GuidedCapturePhase.OpenPort => "BẮT ĐẦU BƯỚC",
        GuidedCapturePhase.WaitForData => "TIẾP TỤC",
        GuidedCapturePhase.TransitionWaitStepOn => "NGƯỜI BẮT ĐẦU BƯỚC LÊN",
        GuidedCapturePhase.TransitionWaitStable => "NGƯỜI ĐÃ ĐỨNG ỔN ĐỊNH",
        GuidedCapturePhase.TransitionWaitStepOff => "NGƯỜI BẮT ĐẦU BƯỚC XUỐNG",
        GuidedCapturePhase.TransitionWaitEmpty => "BÀN CÂN ĐÃ TRỞ LẠI TRỐNG",
        GuidedCapturePhase.SavePrompt => "LƯU PHIÊN",
        GuidedCapturePhase.Completed => "SANG BƯỚC TIẾP",
        _ => "TIẾP TỤC"
    };

    public string GuidedTransitionButtonText => GuidedPrimaryButtonText;

    public ICommand SetManualModeCommand { get; private set; } = null!;
    public ICommand SetGuidedModeCommand { get; private set; } = null!;
    public ICommand GuidedStartStepCommand { get; private set; } = null!;
    public ICommand GuidedCancelSessionCommand { get; private set; } = null!;
    public ICommand GuidedSaveSessionCommand { get; private set; } = null!;
    public ICommand GuidedRetrySessionCommand { get; private set; } = null!;
    public ICommand GuidedNextStepCommand { get; private set; } = null!;
    public ICommand GuidedTransitionMarkerCommand { get; private set; } = null!;
    public ICommand GuidedEmergencyStopCommand { get; private set; } = null!;

    public bool CanCloseApplication()
    {
        if (!IsRecording)
            return true;
        return MessageBox.Show(
            "Đang ghi dữ liệu COM. Bạn có chắc muốn thoát?",
            "CanXe Device Tester",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private SerialPortSettings BuildSettingsRef() => new()
    {
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        Parity = Parity,
        StopBits = StopBits,
        Handshake = Handshake,
        ReadTimeout = ReadTimeout,
        TextEncoding = TextEncoding
    };

    private void UpdateGuidedUi()
    {
        GuidedStepTitle = GetGuidedStepTitle();
        GuidedInstruction = GetGuidedInstruction();
        GuidedConfigSummary = IsGuidedMode
            ? $"Bộ capture hiện tại: {PortName} / {SelectedPreset.SerialConfigLabel}"
            : string.Empty;
        RaiseGuidedCommands();
    }

    private string GetGuidedStepTitle() => _guidedStepIndex switch
    {
        0 => "Bước 1/3 — Bàn cân trống",
        1 => "Bước 2/3 — Một người đứng ổn định",
        2 => "Bước 3/3 — Người bước lên và bước xuống",
        _ => "Hoàn tất guided capture"
    };

    private string GetGuidedInstruction() => (_guidedStepIndex, _guidedPhase) switch
    {
        (0, GuidedCapturePhase.Instructions) => "Bảo đảm bàn cân không có người và không có xe. Nhập số đang hiển thị trên đầu cân.",
        (0, GuidedCapturePhase.EnterWeight) => "Nhập trọng lượng hiển thị trên đầu cân (có thể là 0).",
        (0, GuidedCapturePhase.OpenPort) => "Nhấn BẮT ĐẦU BƯỚC để mở cổng và kiểm tra byte đang nhận.",
        (0, GuidedCapturePhase.WaitForData) => "Xác nhận đã có byte đang nhận, sau đó tiếp tục countdown ổn định 10 giây.",
        (0, GuidedCapturePhase.Stabilizing) => "Đang chờ ổn định trước khi ghi...",
        (0, GuidedCapturePhase.Recording) => "Đang ghi phiên bàn cân trống 30 giây...",
        (1, GuidedCapturePhase.Instructions) => "Yêu cầu một người đứng vào vị trí an toàn trên bàn cân và chờ số ổn định.",
        (1, GuidedCapturePhase.EnterWeight) => "Nhập chính xác số đang hiển thị trên đầu cân.",
        (1, GuidedCapturePhase.Stabilizing) => "Countdown ổn định 10 giây trước khi ghi.",
        (1, GuidedCapturePhase.Recording) => "Đang ghi phiên một người đứng 30 giây...",
        (2, GuidedCapturePhase.Instructions) => "Bắt đầu với bàn cân trống. Ghi sẽ bắt đầu ngay.",
        (2, GuidedCapturePhase.TransitionEmpty) => "Đang ghi trạng thái trống 10 giây...",
        (2, GuidedCapturePhase.TransitionWaitStepOn) => "Khi người bắt đầu bước lên, bấm nút marker.",
        (2, GuidedCapturePhase.TransitionWaitStable) => "Khi số cân ổn định, bấm marker.",
        (2, GuidedCapturePhase.TransitionStableHold) => "Giữ ổn định 20 giây...",
        (2, GuidedCapturePhase.TransitionWaitStepOff) => "Khi người bắt đầu bước xuống, bấm marker.",
        (2, GuidedCapturePhase.TransitionWaitEmpty) => "Khi bàn cân trở về trống, bấm marker.",
        (2, GuidedCapturePhase.TransitionFinalEmpty) => "Ghi thêm 10 giây trạng thái trống...",
        (_, GuidedCapturePhase.ReviewQuality) => "Xem kết quả kiểm tra chất lượng capture.",
        (_, GuidedCapturePhase.SavePrompt) => "Xác nhận lưu phiên này.",
        (_, GuidedCapturePhase.Completed) => "Phiên hoàn tất. Có thể sang bước tiếp hoặc chọn cấu hình khác sau khi đóng cổng.",
        _ => "Chọn BẮT ĐẦU BƯỚC để bắt đầu phiên guided capture."
    };

    private bool CanStartGuidedStep() =>
        IsGuidedMode && !IsRecording && (!_guidedSessionActive || _guidedPhase is GuidedCapturePhase.Instructions or GuidedCapturePhase.EnterWeight or GuidedCapturePhase.OpenPort or GuidedCapturePhase.WaitForData or GuidedCapturePhase.Completed);

    private bool CanAdvanceGuidedPlan() =>
        _guidedPhase == GuidedCapturePhase.Completed && _guidedStepIndex < 2;

    private bool CanPressTransitionMarker() =>
        IsGuidedMode && IsRecording && _guidedStepIndex == 2 && _guidedPhase is
            GuidedCapturePhase.TransitionWaitStepOn or
            GuidedCapturePhase.TransitionWaitStable or
            GuidedCapturePhase.TransitionWaitStepOff or
            GuidedCapturePhase.TransitionWaitEmpty;

    private async Task StartGuidedStepAsync()
    {
        if (!IsGuidedMode)
            return;

        if (_guidedPhase == GuidedCapturePhase.Completed)
        {
            AdvanceGuidedPlan();
            return;
        }

        if (!_guidedSessionActive)
        {
            _guidedSessionActive = true;
            _guidedStepIndex = Math.Max(_guidedStepIndex, 0);
            _guidedPhase = _guidedStepIndex switch
            {
                0 => GuidedCapturePhase.Instructions,
                1 => GuidedCapturePhase.Instructions,
                2 => GuidedCapturePhase.Instructions,
                _ => GuidedCapturePhase.Instructions
            };
        }

        switch (_guidedPhase)
        {
            case GuidedCapturePhase.Instructions when _guidedStepIndex is 0 or 1:
                _guidedPhase = GuidedCapturePhase.EnterWeight;
                break;
            case GuidedCapturePhase.Instructions when _guidedStepIndex == 2:
                await BeginTransitionRecordingAsync();
                break;
            case GuidedCapturePhase.EnterWeight:
                if (_guidedStepIndex is 0 or 1 && !TryParseKnownWeight(out _))
                {
                    MessageBox.Show("Vui lòng nhập trọng lượng hiển thị trên đầu cân.", "Guided Capture");
                    return;
                }
                _guidedPhase = _guidedStepIndex == 1
                    ? (IsPortOpen ? GuidedCapturePhase.Stabilizing : GuidedCapturePhase.OpenPort)
                    : GuidedCapturePhase.OpenPort;
                if (_guidedPhase == GuidedCapturePhase.Stabilizing)
                    BeginCountdown(GuidedCaptureTimings.StableStabilizeSeconds, GuidedCapturePhase.Recording);
                break;
            case GuidedCapturePhase.OpenPort when _guidedStepIndex == 1:
                await OpenPortAsync();
                if (!IsPortOpen)
                    return;
                _guidedPhase = GuidedCapturePhase.Stabilizing;
                BeginCountdown(GuidedCaptureTimings.StableStabilizeSeconds, GuidedCapturePhase.Recording);
                break;
            case GuidedCapturePhase.OpenPort:
                await OpenPortAsync();
                if (!IsPortOpen)
                    return;
                _guidedPhase = GuidedCapturePhase.WaitForData;
                break;
            case GuidedCapturePhase.WaitForData:
                if (_capture.TotalBytesReceived <= 0 && _capture.RecordingTotalBytes <= 0)
                {
                    MessageBox.Show("Chưa nhận byte nào. Kiểm tra cáp, cấu hình hoặc phần mềm cân cũ.", "Guided Capture");
                    return;
                }
                BeginCountdown(GuidedCaptureTimings.StableStabilizeSeconds, GuidedCapturePhase.Recording);
                break;
            case GuidedCapturePhase.SavePrompt:
                await SaveGuidedSessionAsync();
                break;
        }

        UpdateGuidedUi();
        RaiseGuidedCommands();
    }

    private async Task BeginTransitionRecordingAsync()
    {
        if (!IsPortOpen)
        {
            _guidedPhase = GuidedCapturePhase.OpenPort;
            await OpenPortAsync();
            if (!IsPortOpen)
                return;
        }

        ApplyGuidedSessionMetadata(GuidedSessionType.EmptyPersonTransition, null, false);
        PushSessionMetadata();
        StartRecording();
        _capture.AddEventMarker(CaptureEventTypes.CaptureStarted, "Bắt đầu ghi transition");
        _capture.AddEventMarker(CaptureEventTypes.EmptyStableStarted, "Bàn cân trống ban đầu");
        _guidedPhase = GuidedCapturePhase.TransitionEmpty;
        BeginCountdown(GuidedCaptureTimings.TransitionInitialEmptySeconds, GuidedCapturePhase.TransitionWaitStepOn);
    }

    private async Task HandleTransitionMarkerAsync()
    {
        switch (_guidedPhase)
        {
            case GuidedCapturePhase.TransitionWaitStepOn:
                _capture.AddEventMarker(CaptureEventTypes.PersonStepOnStarted, "Người bắt đầu bước lên");
                _guidedPhase = GuidedCapturePhase.TransitionWaitStable;
                break;
            case GuidedCapturePhase.TransitionWaitStable:
                _capture.AddEventMarker(CaptureEventTypes.PersonStableStarted, "Người đã đứng ổn định");
                _guidedPhase = GuidedCapturePhase.TransitionStableHold;
                BeginCountdown(GuidedCaptureTimings.TransitionStableHoldSeconds, GuidedCapturePhase.TransitionWaitStepOff);
                break;
            case GuidedCapturePhase.TransitionWaitStepOff:
                _capture.AddEventMarker(CaptureEventTypes.PersonStepOffStarted, "Người bắt đầu bước xuống");
                _guidedPhase = GuidedCapturePhase.TransitionWaitEmpty;
                break;
            case GuidedCapturePhase.TransitionWaitEmpty:
                _capture.AddEventMarker(CaptureEventTypes.EmptyRestored, "Bàn cân đã trở lại trống");
                _guidedPhase = GuidedCapturePhase.TransitionFinalEmpty;
                BeginCountdown(GuidedCaptureTimings.TransitionFinalEmptySeconds, GuidedCapturePhase.ReviewQuality);
                break;
        }

        UpdateGuidedUi();
        RaiseGuidedCommands();
        await Task.CompletedTask;
    }

    private void BeginCountdown(int seconds, GuidedCapturePhase phaseAfterCountdown)
    {
        _postCountdownPhase = phaseAfterCountdown;
        if (phaseAfterCountdown == GuidedCapturePhase.Recording && _guidedStepIndex is 0 or 1)
            _guidedPhase = GuidedCapturePhase.Stabilizing;

        CountdownSeconds = seconds;
        _guidedTimer.Start();
        UpdateGuidedUi();
    }

    private void OnGuidedTimerTick()
    {
        if (CountdownSeconds > 0)
        {
            CountdownSeconds--;
            OnPropertyChanged(nameof(CountdownSeconds));
            return;
        }

        _guidedTimer.Stop();

        switch (_guidedPhase)
        {
            case GuidedCapturePhase.Stabilizing:
                BeginStableRecording();
                break;
            case GuidedCapturePhase.Recording:
                FinishRecordingAndReview();
                break;
            case GuidedCapturePhase.TransitionEmpty:
                _guidedPhase = GuidedCapturePhase.TransitionWaitStepOn;
                break;
            case GuidedCapturePhase.TransitionStableHold:
                _guidedPhase = GuidedCapturePhase.TransitionWaitStepOff;
                break;
            case GuidedCapturePhase.TransitionFinalEmpty:
                FinishRecordingAndReview();
                break;
        }

        UpdateGuidedUi();
        RaiseGuidedCommands();
    }

    private void BeginStableRecording()
    {
        var sessionType = _guidedStepIndex == 0 ? GuidedSessionType.EmptyStable : GuidedSessionType.PersonStable;
        ApplyGuidedSessionMetadata(sessionType, GuidedKnownWeightKg, true);
        PushSessionMetadata();
        if (!IsRecording)
        {
            StartRecording();
            _capture.AddEventMarker(CaptureEventTypes.CaptureStarted, "Bắt đầu ghi stable");
            if (sessionType == GuidedSessionType.EmptyStable)
                _capture.AddEventMarker(CaptureEventTypes.EmptyStableStarted, "Bàn cân trống ổn định");
        }

        _guidedPhase = GuidedCapturePhase.Recording;
        CountdownSeconds = GuidedCaptureTimings.StableRecordSeconds;
        _guidedTimer.Start();
        UpdateGuidedUi();
    }

    private void FinishRecordingAndReview()
    {
        _guidedTimer.Stop();
        StopRecording();
        _capture.AddEventMarker(CaptureEventTypes.CaptureStopped, "Dừng ghi");
        _guidedPhase = GuidedCapturePhase.ReviewQuality;
        BuildPendingQualityPreview();
        _guidedAwaitingSaveConfirm = true;
        _guidedPhase = GuidedCapturePhase.SavePrompt;
        UpdateGuidedUi();
        RaiseGuidedCommands();
    }

    private void BuildPendingQualityPreview()
    {
        _pendingStats = BuildRecordingStats(pendingSave: false);
        _lastQuality = CaptureQualityValidator.Validate(_pendingStats);
        QualityReport = FormatQualityReport(_pendingStats, _lastQuality);
    }

    private async Task SaveGuidedSessionAsync()
    {
        if (_pendingStats is null)
            BuildPendingQualityPreview();

        var stats = _pendingStats!;
        if (stats.SessionType is GuidedSessionType sessionType)
        {
            var isStable = sessionType is GuidedSessionType.EmptyStable or GuidedSessionType.PersonStable;
            decimal? known = isStable && TryParseKnownWeight(out var w) ? w : null;
            ApplyGuidedSessionMetadata(sessionType, known, isStable);
        }

        var options = BuildSaveOptions(stats);
        var result = await _capture.SaveLogAsync(LogDirectory, options);
        var rawBytes = _capture.GetRecordedRawBytes();
        var rawSize = result.RawBinaryPath is not null && File.Exists(result.RawBinaryPath)
            ? new FileInfo(result.RawBinaryPath).Length
            : 0;

        stats = stats with
        {
            TextLogSaved = File.Exists(result.TextLogPath),
            RawFileSaved = result.RawBinaryPath is not null && File.Exists(result.RawBinaryPath),
            SessionJsonSaved = result.SessionJsonPath is not null && File.Exists(result.SessionJsonPath),
            RawFileSize = rawSize
        };

        var quality = CaptureQualityValidator.Validate(stats);
        _lastQuality = quality;
        QualityReport = FormatQualityReport(stats, quality);
        LastSavedLogPath = $"{result.TextLogPath} | raw: {result.RawBinaryPath} | json: {result.SessionJsonPath}";

        _guidedSavedSessions.Add(new GuidedSavedSession
        {
            SessionType = stats.SessionType!.Value,
            SaveResult = result,
            Stats = stats,
            Quality = quality,
            RawBytes = rawBytes
        });

        UpdateComparisonReport();
        _guidedAwaitingSaveConfirm = false;
        _guidedSessionActive = false;
        _guidedPhase = GuidedCapturePhase.Completed;
        _pendingSaveResult = result;
        _pendingStats = null;
        UpdateGuidedUi();
        RaiseGuidedCommands();
    }

    private void CancelGuidedSession()
    {
        _guidedTimer.Stop();
        if (IsRecording)
        {
            StopRecording();
            _capture.DiscardRecording();
        }

        _guidedSessionActive = false;
        _guidedAwaitingSaveConfirm = false;
        _guidedPhase = GuidedCapturePhase.Idle;
        _pendingStats = null;
        QualityReport = string.Empty;
        UpdateGuidedUi();
        RaiseGuidedCommands();
    }

    private void RetryGuidedSession()
    {
        CancelGuidedSession();
        _guidedSessionActive = true;
        _guidedPhase = GuidedCapturePhase.Instructions;
        UpdateGuidedUi();
    }

    private void AdvanceGuidedPlan()
    {
        if (_guidedStepIndex >= 2)
            return;

        _guidedStepIndex++;
        _guidedPhase = GuidedCapturePhase.Instructions;
        _guidedSessionActive = false;
        GuidedKnownWeightKg = null;
        QualityReport = string.Empty;
        UpdateGuidedUi();
    }

    private void EmergencyStopGuided()
    {
        _guidedTimer.Stop();
        StopRecording();
        _capture.AddEventMarker(CaptureEventTypes.CaptureStopped, "Dừng khẩn cấp");
        FinishRecordingAndReview();
    }

    private void ApplyGuidedSessionMetadata(GuidedSessionType sessionType, decimal? knownWeightKg, bool isStable)
    {
        if (sessionType != GuidedSessionType.EmptyPersonTransition
            && knownWeightKg is null
            && TryParseKnownWeight(out var parsed))
        {
            knownWeightKg = parsed;
        }

        if (sessionType == GuidedSessionType.EmptyPersonTransition)
            knownWeightKg = null;

        var label = sessionType switch
        {
            GuidedSessionType.EmptyStable => "Bàn cân trống ổn định",
            GuidedSessionType.PersonStable => "Một người đứng ổn định",
            GuidedSessionType.EmptyPersonTransition => "Trống - người lên - ổn định - người xuống - trống",
            _ => SelectedSessionLabel
        };

        ScaleDisplayWeight = knownWeightKg?.ToString(CultureInfo.InvariantCulture);
        SelectedSessionLabel = label;
        _capture.UpdateSessionMetadata(new SerialCaptureSession
        {
            PortSettings = BuildSettings(),
            SessionLabel = label,
            SessionType = sessionType,
            KnownWeightKg = knownWeightKg,
            KnownWeightIsApproximate = sessionType == GuidedSessionType.EmptyPersonTransition,
            IsStableSession = isStable,
            ScaleDisplayWeight = ScaleDisplayWeight
        });
    }

    private bool TryParseKnownWeight(out decimal weight)
    {
        weight = 0;
        if (GuidedKnownWeightKg is not null)
        {
            weight = GuidedKnownWeightKg.Value;
            return true;
        }

        return decimal.TryParse(ScaleDisplayWeight, NumberStyles.Number, CultureInfo.InvariantCulture, out weight)
               || decimal.TryParse(ScaleDisplayWeight, NumberStyles.Number, CultureInfo.CurrentCulture, out weight);
    }

    private CaptureRecordingStats BuildRecordingStats(bool pendingSave)
    {
        var started = _capture.RecordingStartedAt ?? DateTimeOffset.Now;
        var duration = DateTimeOffset.Now - started;
        var sessionType = _guidedStepIndex switch
        {
            0 => GuidedSessionType.EmptyStable,
            1 => GuidedSessionType.PersonStable,
            2 => GuidedSessionType.EmptyPersonTransition,
            _ => (GuidedSessionType?)null
        };

        var hasWeight = TryParseKnownWeight(out var w);
        return new CaptureRecordingStats
        {
            Duration = duration,
            TotalBytes = _capture.RecordingTotalBytes,
            TotalChunks = _capture.RecordingChunkCount,
            BytesPerSecond = duration.TotalSeconds <= 0 ? 0 : _capture.RecordingTotalBytes / duration.TotalSeconds,
            UniqueByteValues = _capture.RecordingUniqueBytes,
            LongestNoDataGapMs = _capture.RecordingLongestNoDataGapMs,
            PortErrorDuringCapture = _capture.PortErrorDuringRecording,
            TextLogSaved = !pendingSave,
            RawFileSaved = !pendingSave,
            SessionJsonSaved = !pendingSave,
            SessionType = sessionType,
            IsStableSession = sessionType is GuidedSessionType.EmptyStable or GuidedSessionType.PersonStable,
            KnownWeightKg = hasWeight ? w : null,
            HasKnownWeight = hasWeight,
            Events = _capture.EventMarkers
        };
    }

    private CaptureSaveOptions BuildSaveOptions(CaptureRecordingStats stats) => new()
    {
        PortSettings = BuildSettings(),
        SessionType = stats.SessionType,
        Timestamp = DateTimeOffset.Now,
        Events = _capture.EventMarkers,
        TotalChunks = stats.TotalChunks,
        UniqueByteValues = stats.UniqueByteValues,
        LongestNoDataGapMs = stats.LongestNoDataGapMs,
        PortErrorDuringCapture = stats.PortErrorDuringCapture,
        RecordingStartedAt = _capture.RecordingStartedAt,
        RecordingEndedAt = _capture.RecordingEndedAt
    };

    private void UpdateComparisonReport()
    {
        var empty = _guidedSavedSessions.FirstOrDefault(s => s.SessionType == GuidedSessionType.EmptyStable);
        var person = _guidedSavedSessions.FirstOrDefault(s => s.SessionType == GuidedSessionType.PersonStable);
        if (empty is null || person is null)
        {
            ComparisonReport = string.Empty;
            return;
        }

        var cmp = CaptureStreamComparer.Compare(empty.RawBytes, person.RawBytes);
        ComparisonReport =
            $"Empty total bytes: {cmp.EmptyTotalBytes}{Environment.NewLine}" +
            $"Person total bytes: {cmp.PersonTotalBytes}{Environment.NewLine}" +
            $"Empty unique bytes: {cmp.EmptyUniqueByteCount}{Environment.NewLine}" +
            $"Person unique bytes: {cmp.PersonUniqueByteCount}{Environment.NewLine}" +
            $"Raw streams identical: {(cmp.RawStreamsIdentical ? "yes" : "no")}{Environment.NewLine}" +
            $"Estimated difference %: {cmp.EstimatedDifferencePercentage:F2}{Environment.NewLine}" +
            $"Kết luận: {cmp.Summary}";
    }

    private static string FormatQualityReport(CaptureRecordingStats stats, CaptureQualityResult quality)
    {
        var status = quality.Status switch
        {
            CaptureQualityStatus.Valid => "Capture hợp lệ",
            CaptureQualityStatus.Warning => "Capture có cảnh báo",
            _ => "Capture không hợp lệ"
        };

        var unique = string.Join(", ", stats.UniqueByteValues.Select(b => b.ToString("X2")));
        return string.Join(Environment.NewLine,
        [
            $"Trạng thái: {status}",
            $"Duration: {stats.Duration.TotalSeconds:F1}s",
            $"Total bytes: {stats.TotalBytes}",
            $"Total chunks: {stats.TotalChunks}",
            $"Bytes/s: {stats.BytesPerSecond:F1}",
            $"Unique byte count: {stats.UniqueByteValues.Count}",
            $"Unique byte values: {unique}",
            $"Longest no-data gap: {stats.LongestNoDataGapMs:F0} ms",
            $"Text log saved: {stats.TextLogSaved}",
            $"Raw file saved: {stats.RawFileSaved}",
            $"Session JSON saved: {stats.SessionJsonSaved}",
            ..quality.Messages.Select(m => $"• {m}")
        ]);
    }

    private void RaiseGuidedCommands()
    {
        (GuidedStartStepCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GuidedCancelSessionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GuidedSaveSessionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GuidedRetrySessionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GuidedNextStepCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GuidedTransitionMarkerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GuidedEmergencyStopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsModeSelectionEnabled));
        OnPropertyChanged(nameof(IsPresetSelectionEnabled));
        OnPropertyChanged(nameof(GuidedPrimaryButtonText));
        OnPropertyChanged(nameof(GuidedTransitionButtonText));
        OnPropertyChanged(nameof(GuidedEventMarkers));
    }

    partial void RefreshGuidedFromService()
    {
        if (!IsGuidedMode)
            return;
        RaiseGuidedCommands();
    }
}
