using CanXe.Domain.Models;

using CanXe.Domain.Services;

using CanXe.ScaleProtocol.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;



namespace CanXe.Desktop.ViewModels;



public sealed partial class MainViewModel

{

    [ObservableProperty] private bool _isHardwareModeSelected;

    [ObservableProperty] private string _hardwareConnectionStatus = "Chưa kết nối";

    [ObservableProperty] private string _hardwareStableText = "● CHƯA ỔN ĐỊNH";

    [ObservableProperty] private string? _hardwareLastFrameText;

    [ObservableProperty] private string? _hardwareChecksumWarning;

    [ObservableProperty] private bool _isHardwareConnected;

    [ObservableProperty] private bool _isHardwareConnectEnabled = true;

    [ObservableProperty] private bool _isHardwareDisconnectEnabled;



    public string EffectiveDeviceMode =>

        !string.IsNullOrWhiteSpace(_settings.DeviceMode)

            ? _settings.DeviceMode

            : Settings.DeviceMode;



    public bool IsHardwareModeEnabled =>

        ScaleInputModeDisplay.IsHardwareOptionEnabled(EffectiveDeviceMode);



    public bool IsScaleSourceSelectionVisible => true;



    public bool IsHardwareOperationsPanelVisible =>
        ScaleInputMode == ScaleInputMode.Hardware;

    [RelayCommand]
    private async Task SelectHardwareModeAsync() =>
        await ApplyScaleInputModeAsync(ScaleInputMode.Hardware, userInitiated: true, persistSettings: false);



    [RelayCommand]

    private async Task ConnectHardwareAsync()

    {

        _disconnectedByUser = false;

        _autoConnectCts?.Cancel();

        await ConnectHardwareInternalAsync().ConfigureAwait(false);

    }



    [RelayCommand]

    private async Task DisconnectHardwareAsync()

    {

        if (_hardwareScale is null)

            return;



        _disconnectedByUser = true;

        _autoConnectCts?.Cancel();

        await _hardwareScale.DisconnectHardwareAsync().ConfigureAwait(false);

        _hasReceivedHardwareFrame = false;

        LiveWeightKg = 0;

        OperatorStatusMessage = "Đã ngắt kết nối đầu cân COM.";

        UpdateHardwareDiagnostics();

        RefreshLiveWeightDisplay();

        OnPropertyChanged(nameof(IsRetryConnectVisible));

    }



    private ScaleSerialSettings BuildHardwareSerialSettings() => new()

    {

        PortName = Settings.PortName,

        BaudRate = Settings.BaudRate,

        DataBits = Settings.DataBits,

        Parity = Settings.Parity,

        StopBits = Settings.StopBits,

        Handshake = Settings.Handshake

    };



    private void UpdateHardwareDiagnostics()

    {

        if (_hardwareScale is null || ScaleInputMode != ScaleInputMode.Hardware)

            return;



        IsHardwareConnected = _hardwareScale.IsConnected;

        IsHardwareConnectEnabled = ScaleInputMode == ScaleInputMode.Hardware && !IsHardwareConnected && !IsScaleConnecting;

        IsHardwareDisconnectEnabled = IsHardwareConnected;

        HardwareConnectionStatus = _hardwareScale.ConnectionState switch

        {

            ScaleConnectionState.Connected when _hardwareScale.IsStale => "MẤT KẾT NỐI — dữ liệu quá hạn",

            ScaleConnectionState.Connected => "Đã kết nối",

            ScaleConnectionState.Connecting => "Đang kết nối...",

            ScaleConnectionState.Error => "Lỗi kết nối",

            _ => "Chưa kết nối"

        };

        HardwareStableText = IsScaleConnecting || !_hardwareScale.IsConnected

            ? "● CHỜ DỮ LIỆU"

            : _hardwareScale.IsStale

                ? "● MẤT KẾT NỐI"

                : !_hasReceivedHardwareFrame || _hardwareScale.LatestReading is null

                    ? "● CHỜ DỮ LIỆU"

                    : _hardwareScale.IsStable

                        ? "● ỔN ĐỊNH"

                        : "● CHƯA ỔN ĐỊNH";

        HardwareLastFrameText = _hardwareScale.GetLatestFrameDisplay();

        HardwareChecksumWarning = _hardwareScale.LastChecksumError;



        if (_hardwareScale.IsConnected && !_hardwareScale.IsStale && _hardwareScale.LatestReading is not null)

        {

            _hasReceivedHardwareFrame = true;

            LiveWeightKg = _hardwareScale.LatestReading.WeightKg;

        }

        else if (!_hasReceivedHardwareFrame)

            LiveWeightKg = 0;



        UpdateScaleStatusDisplay();

        UpdateButtonStates();

        RefreshLiveWeightDisplay();

        OnPropertyChanged(nameof(IsRetryConnectVisible));

    }



    private bool TryBlockHardwareCapture(out string message)

    {

        message = string.Empty;

        if (ScaleInputMode != ScaleInputMode.Hardware || _hardwareScale is null)

            return false;



        var reason = _hardwareScale.GetHardwareCaptureBlockReason();

        if (reason is null)

            return false;



        message = reason;

        return true;

    }

}


