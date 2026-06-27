using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CanXe.DeviceTester.Core;
using CanXe.DeviceTester.Core.Models;
using CanXe.DeviceTester.Core.Services;
using Microsoft.Win32;

namespace CanXe.DeviceTester.ViewModels;

public partial class DeviceTesterViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISerialCaptureService _capture;
    private readonly System.Windows.Threading.DispatcherTimer _uiTimer;
    private string _portName = "COM1";
    private int _baudRate = 9600;
    private int _dataBits = 8;
    private string _parity = "None";
    private string _stopBits = "One";
    private string _handshake = "None";
    private int _readTimeout = 500;
    private SerialTextEncodingOption _textEncoding = SerialTextEncodingOption.Ascii;
    private string _selectedSessionLabel = SessionLabelPresets.NoVehicle;
    private string? _scaleDisplayWeight;
    private string? _vehicleCondition;
    private string? _sessionStartNote;
    private string? _additionalNotes;
    private string _statusDisplay = "Cổng đang đóng.";
    private string _latestHex = string.Empty;
    private string _latestEscaped = string.Empty;
    private string _eventChunks = string.Empty;
    private string _accumulatedHex = string.Empty;
    private string _accumulatedEscaped = string.Empty;
    private string _logDirectory = GetDefaultLogDirectory();
    private string? _lastSavedLogPath;
    private bool _isDisplayPaused;
    private bool _saveRawBinary = true;
    private bool _saveSessionJson = true;

    public DeviceTesterViewModel(ISerialCaptureService capture)
    {
        _capture = capture;
        _capture.SaveRawBinary = _saveRawBinary;
        _capture.SaveSessionJson = _saveSessionJson;
        _capture.StateChanged += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(RefreshFromService);

        ScanPortsCommand = new RelayCommand(ScanPorts);
        OpenPortCommand = new RelayCommand(async () => await OpenPortAsync(), () => !IsPortOpen);
        ClosePortCommand = new RelayCommand(async () => await ClosePortAsync(), () => IsPortOpen);
        StartRecordingCommand = new RelayCommand(StartRecording, () => IsPortOpen);
        StopRecordingCommand = new RelayCommand(StopRecording, () => IsRecording);
        SaveLogCommand = new RelayCommand(async () => await SaveLogAsync(), () => IsRecording || !string.IsNullOrEmpty(_eventChunks));
        ClearDisplayCommand = new RelayCommand(ClearDisplay);
        TogglePauseDisplayCommand = new RelayCommand(TogglePauseDisplay, () => IsPortOpen);
        ChooseLogFolderCommand = new RelayCommand(ChooseLogFolder);

        InitializeGuidedCapture();
        InitializeScaleDecode();

        _uiTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _uiTimer.Tick += (_, _) =>
        {
            _capture.ApplyPendingUiUpdates();
            RefreshFromService();
        };
        _uiTimer.Start();

        RefreshFromService();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> AvailablePorts { get; private set; } = [];
    public IReadOnlyList<string> SessionLabels => SessionLabelPresets.All;
    public IReadOnlyList<string> ParityOptions { get; } = ["None", "Odd", "Even", "Mark", "Space"];
    public IReadOnlyList<string> StopBitsOptions { get; } = ["One", "Two", "OnePointFive"];
    public IReadOnlyList<string> HandshakeOptions { get; } = ["None", "XOnXOff", "RequestToSend", "RequestToSendXOnXOff"];
    public IReadOnlyList<SerialTextEncodingOption> EncodingOptions { get; } =
        [SerialTextEncodingOption.Ascii, SerialTextEncodingOption.Utf8, SerialTextEncodingOption.Windows1258];

    public string PortName { get => _portName; set => SetField(ref _portName, value); }
    public int BaudRate { get => _baudRate; set => SetField(ref _baudRate, value); }
    public int DataBits { get => _dataBits; set => SetField(ref _dataBits, value); }
    public string Parity { get => _parity; set => SetField(ref _parity, value); }
    public string StopBits { get => _stopBits; set => SetField(ref _stopBits, value); }
    public string Handshake { get => _handshake; set => SetField(ref _handshake, value); }
    public int ReadTimeout { get => _readTimeout; set => SetField(ref _readTimeout, value); }
    public SerialTextEncodingOption TextEncoding { get => _textEncoding; set => SetField(ref _textEncoding, value); }

    public string SelectedSessionLabel
    {
        get => _selectedSessionLabel;
        set
        {
            if (SetField(ref _selectedSessionLabel, value))
                PushSessionMetadata();
        }
    }

    public string? ScaleDisplayWeight
    {
        get => _scaleDisplayWeight;
        set { if (SetField(ref _scaleDisplayWeight, value)) PushSessionMetadata(); }
    }

    public string? VehicleCondition
    {
        get => _vehicleCondition;
        set { if (SetField(ref _vehicleCondition, value)) PushSessionMetadata(); }
    }

    public string? SessionStartNote
    {
        get => _sessionStartNote;
        set { if (SetField(ref _sessionStartNote, value)) PushSessionMetadata(); }
    }

    public string? AdditionalNotes
    {
        get => _additionalNotes;
        set { if (SetField(ref _additionalNotes, value)) PushSessionMetadata(); }
    }

    public bool IsConfigurationEnabled => !IsPortOpen;
    public bool IsPortOpen => _capture.IsPortOpen;
    public bool IsRecording => _capture.IsRecording;
    public bool IsDisplayPaused
    {
        get => _isDisplayPaused;
        private set => SetField(ref _isDisplayPaused, value);
    }

    public string StatusDisplay { get => _statusDisplay; private set => SetField(ref _statusDisplay, value); }
    public string LatestHex { get => _latestHex; private set => SetField(ref _latestHex, value); }
    public string LatestEscaped { get => _latestEscaped; private set => SetField(ref _latestEscaped, value); }
    public string EventChunks { get => _eventChunks; private set => SetField(ref _eventChunks, value); }
    public string AccumulatedHex { get => _accumulatedHex; private set => SetField(ref _accumulatedHex, value); }
    public string AccumulatedEscaped { get => _accumulatedEscaped; private set => SetField(ref _accumulatedEscaped, value); }
    public string LogDirectory { get => _logDirectory; private set => SetField(ref _logDirectory, value); }
    public string? LastSavedLogPath { get => _lastSavedLogPath; private set => SetField(ref _lastSavedLogPath, value); }

    public bool SaveRawBinary
    {
        get => _saveRawBinary;
        set
        {
            if (SetField(ref _saveRawBinary, value))
                _capture.SaveRawBinary = value;
        }
    }

    public bool SaveSessionJson
    {
        get => _saveSessionJson;
        set
        {
            if (SetField(ref _saveSessionJson, value))
                _capture.SaveSessionJson = value;
        }
    }

    public string StatsLine { get; private set; } = string.Empty;
    public string WarningBanner { get; } =
        "Chỉ chạy công cụ này khi phần mềm cân cũ đã được đóng hoàn toàn. Hai ứng dụng thường không thể cùng sử dụng COM1.";

    public ICommand ScanPortsCommand { get; }
    public ICommand OpenPortCommand { get; }
    public ICommand ClosePortCommand { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand SaveLogCommand { get; }
    public ICommand ClearDisplayCommand { get; }
    public ICommand TogglePauseDisplayCommand { get; }
    public ICommand ChooseLogFolderCommand { get; }

    public void Dispose()
    {
        _uiTimer.Stop();
        _capture.Dispose();
    }

    private void ScanPorts()
    {
        AvailablePorts = _capture.ScanPorts();
        OnPropertyChanged(nameof(AvailablePorts));
    }

    private async Task OpenPortAsync()
    {
        try
        {
            _capture.UpdateSettings(BuildSettings());
            PushSessionMetadata();
            await _capture.OpenPortAsync();
        }
        catch
        {
            RefreshFromService();
        }
    }

    private async Task ClosePortAsync()
    {
        await _capture.ClosePortAsync();
        ResetScaleDecode();
        RefreshFromService();
    }

    private void StartRecording()
    {
        PushSessionMetadata();
        _capture.StartRecording();
        RefreshFromService();
    }

    private void StopRecording()
    {
        _capture.StopRecording();
        RefreshFromService();
    }

    private async Task SaveLogAsync()
    {
        var result = await _capture.SaveLogAsync(LogDirectory);
        LastSavedLogPath = result.RawBinaryPath is not null || result.SessionJsonPath is not null
            ? $"{result.TextLogPath} | raw: {result.RawBinaryPath ?? "—"} | json: {result.SessionJsonPath ?? "—"}"
            : result.TextLogPath;
        RefreshFromService();
    }

    private void ClearDisplay()
    {
        _capture.ClearDisplay();
        ResetScaleDecode();
        RefreshFromService();
    }

    private void TogglePauseDisplay()
    {
        IsDisplayPaused = !IsDisplayPaused;
        _capture.IsDisplayPaused = IsDisplayPaused;
        RefreshFromService();
    }

    private void ChooseLogFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục lưu log" };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            LogDirectory = dialog.FolderName;
    }

    private void PushSessionMetadata()
    {
        _capture.UpdateSessionMetadata(new SerialCaptureSession
        {
            PortSettings = BuildSettings(),
            SessionLabel = SelectedSessionLabel,
            ScaleDisplayWeight = ScaleDisplayWeight,
            VehicleCondition = VehicleCondition,
            SessionStartNote = SessionStartNote,
            AdditionalNotes = AdditionalNotes
        });
    }

    private SerialPortSettings BuildSettings() => new()
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

    private void RefreshFromService()
    {
        StatusDisplay = FormatStatus(_capture.Status, _capture.StatusMessage);
        LatestHex = _capture.LatestHexLine;
        LatestEscaped = _capture.LatestEscapedText;
        EventChunks = _capture.EventChunksText;
        var buffer = _capture.AccumulatedBuffer;
        AccumulatedHex = CanXe.DeviceTester.Core.Formatting.SerialHexFormatter.ToHexString(buffer);
        AccumulatedEscaped = CanXe.DeviceTester.Core.Formatting.SerialTextEscaper.Escape(buffer);
        StatsLine =
            $"Byte đã nhận: {_capture.TotalBytesReceived} | DataReceived: {_capture.DataReceivedCount} | " +
            $"Byte/giây: {_capture.BytesPerSecondRecent:F1} | " +
            $"Lần nhận gần nhất: {(_capture.LastDataReceivedAt?.ToString("HH:mm:ss.fff") ?? "—")} | " +
            $"Thời gian phiên: {_capture.SessionElapsed:hh\\:mm\\:ss} | " +
            $"Ghi log: {(IsRecording ? "BẬT" : "TẮT")} | Pause hiển thị: {(IsDisplayPaused ? "BẬT" : "TẮT")}";

        OnPropertyChanged(nameof(IsPortOpen));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsConfigurationEnabled));
        OnPropertyChanged(nameof(StatsLine));
        (OpenPortCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClosePortCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StartRecordingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StopRecordingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveLogCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (TogglePauseDisplayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        RefreshGuidedFromService();
    }

    private static string FormatStatus(SerialConnectionStatus status, string? detail) => status switch
    {
        SerialConnectionStatus.Closed => "Cổng đang đóng.",
        SerialConnectionStatus.Connecting => "Đang kết nối...",
        SerialConnectionStatus.Open => detail ?? "Đã mở cổng.",
        SerialConnectionStatus.NoData => "Không có dữ liệu.",
        SerialConnectionStatus.Receiving => "Đang nhận dữ liệu.",
        SerialConnectionStatus.PortBusy => detail ?? "Cổng đang bị ứng dụng khác sử dụng.",
        SerialConnectionStatus.Disconnected => detail ?? "Mất kết nối.",
        SerialConnectionStatus.ReadError => detail ?? "Lỗi đọc.",
        _ => detail ?? status.ToString()
    };

    private static string GetDefaultLogDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CanXeDeviceTester", "Logs");

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    partial void InitializeGuidedCapture();
    partial void InitializeScaleDecode();
    partial void ResetScaleDecode();
    partial void RefreshGuidedFromService();
}

internal sealed class RelayCommand : ICommand
{
    private readonly Func<Task>? _asyncExecute;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<Task> asyncExecute, Func<bool>? canExecute = null)
    {
        _asyncExecute = asyncExecute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (_asyncExecute is not null)
            await _asyncExecute();
        else
            _execute?.Invoke();
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
