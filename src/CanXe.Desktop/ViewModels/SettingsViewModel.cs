using System.IO;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Infrastructure.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly StationSettingsService _settingsService;
    private readonly ICameraConnectionTester _cameraTester;
    private readonly IScaleConnectionTester _scaleTester;
    private readonly AppSettings _appSettings;

    public SettingsViewModel(
        StationSettingsService settingsService,
        ICameraConnectionTester cameraTester,
        IScaleConnectionTester scaleTester,
        AppSettings appSettings)
    {
        _settingsService = settingsService;
        _cameraTester = cameraTester;
        _scaleTester = scaleTester;
        _appSettings = appSettings;
    }

    [ObservableProperty] private string _stationName = "Trạm cân CanXe";
    [ObservableProperty] private string? _ownerName;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _taxCode;
    [ObservableProperty] private string? _logoPath;
    [ObservableProperty] private string? _ticketFooterText;
    [ObservableProperty] private string? _stationNameError;

    [ObservableProperty] private string _deviceMode = "Simulation";
    [ObservableProperty] private string _portName = "COM1";
    [ObservableProperty] private int _baudRate = 9600;
    [ObservableProperty] private int _dataBits = 8;
    [ObservableProperty] private string _parity = "None";
    [ObservableProperty] private string _stopBits = "One";
    [ObservableProperty] private string _handshake = "None";
    [ObservableProperty] private string? _scaleTestMessage;

    [ObservableProperty] private string? _cameraName;
    [ObservableProperty] private bool _cameraEnabled = true;
    [ObservableProperty] private string? _rtspUrl;
    [ObservableProperty] private string? _cameraUsername;
    [ObservableProperty] private string? _cameraPassword;
    [ObservableProperty] private bool _showCameraPassword;
    [ObservableProperty] private bool _cameraPreviewEnabled = true;
    [ObservableProperty] private bool _autoConnectionCheck = true;
    [ObservableProperty] private int _snapshotTimeoutSeconds = 5;
    [ObservableProperty] private int _photoRetentionDays = 3;
    [ObservableProperty] private string? _rtspUrlError;
    [ObservableProperty] private string? _photoRetentionError;
    [ObservableProperty] private string? _cameraTestMessage;
    [ObservableProperty] private string? _settingsStatusMessage;

    [ObservableProperty] private string _applicationVersion = "1.7.0";
    [ObservableProperty] private string _databasePath = string.Empty;
    [ObservableProperty] private string _deviceModeDisplay = "Simulation";
    [ObservableProperty] private string _migrationStatus = "—";
    [ObservableProperty] private string _errorLogSummary = "Không có lỗi gần đây.";

    public event EventHandler<StationSettingsDto>? StationSettingsSaved;

    public async Task LoadAsync(string databasePath)
    {
        DatabasePath = databasePath;
        DeviceModeDisplay = _appSettings.DeviceMode;
        MigrationStatus = $"{DatabaseUpgrader.Phase16MigrationId}, {DatabaseUpgrader.Phase17MigrationId}";

        var station = await _settingsService.GetStationAsync();
        StationName = station.StationName;
        OwnerName = station.OwnerName;
        Address = station.Address;
        Phone = station.Phone;
        Email = station.Email;
        TaxCode = station.TaxCode;
        LogoPath = station.LogoPath;
        TicketFooterText = station.TicketFooterText;

        var scale = await _settingsService.GetScaleAsync();
        DeviceMode = scale.DeviceMode;
        PortName = scale.PortName;
        BaudRate = scale.BaudRate;
        DataBits = scale.DataBits;
        Parity = scale.Parity;
        StopBits = scale.StopBits;
        Handshake = scale.Handshake;

        var camera = await _settingsService.GetCameraAsync();
        CameraName = camera.CameraName;
        CameraEnabled = camera.IsEnabled;
        RtspUrl = camera.RtspUrl;
        CameraUsername = camera.Username;
        CameraPassword = camera.Password;
        CameraPreviewEnabled = camera.PreviewEnabled;
        AutoConnectionCheck = camera.AutoConnectionCheck;
        SnapshotTimeoutSeconds = camera.SnapshotTimeoutSeconds;
        PhotoRetentionDays = camera.PhotoRetentionDays;
    }

    [RelayCommand]
    private async Task SaveStationAsync()
    {
        StationNameError = null;
        var dto = BuildStationDto();
        var (success, validation) = await _settingsService.SaveStationAsync(dto);
        if (!success)
        {
            StationNameError = validation.Errors.GetValueOrDefault(nameof(StationSettingsDto.StationName));
            SettingsStatusMessage = "Không lưu được — kiểm tra các trường lỗi.";
            return;
        }

        SettingsStatusMessage = "Đã lưu thông tin trạm cân.";
        StationSettingsSaved?.Invoke(this, dto);
    }

    [RelayCommand]
    private async Task SaveScaleAsync()
    {
        await _settingsService.SaveScaleAsync(new ScaleDeviceSettingsDto
        {
            DeviceMode = DeviceMode,
            PortName = PortName,
            BaudRate = BaudRate,
            DataBits = DataBits,
            Parity = Parity,
            StopBits = StopBits,
            Handshake = Handshake
        });
        SettingsStatusMessage = "Đã lưu cấu hình đầu cân.";
    }

    [RelayCommand]
    private async Task SaveCameraAsync()
    {
        RtspUrlError = null;
        PhotoRetentionError = null;
        var dto = BuildCameraDto();
        var (success, validation) = await _settingsService.SaveCameraAsync(dto);
        if (!success)
        {
            RtspUrlError = validation.Errors.GetValueOrDefault(nameof(CameraDeviceSettingsDto.RtspUrl));
            PhotoRetentionError = validation.Errors.GetValueOrDefault(nameof(CameraDeviceSettingsDto.PhotoRetentionDays));
            SettingsStatusMessage = "Không lưu được cấu hình camera.";
            return;
        }

        SettingsStatusMessage = "Đã lưu cấu hình camera.";
    }

    [RelayCommand]
    private async Task TestRtspAsync()
    {
        CameraTestMessage = null;
        var result = await _cameraTester.TestAsync(BuildCameraDto());
        CameraTestMessage = result.Message;
    }

    [RelayCommand]
    private async Task TestScaleAsync()
    {
        ScaleTestMessage = null;
        var result = await _scaleTester.TestAsync(new ScaleDeviceSettingsDto
        {
            DeviceMode = DeviceMode,
            PortName = PortName,
            BaudRate = BaudRate,
            DataBits = DataBits,
            Parity = Parity,
            StopBits = StopBits,
            Handshake = Handshake
        });
        ScaleTestMessage = result.Message;
    }

    [RelayCommand]
    private void BackupDatabase()
    {
        if (string.IsNullOrWhiteSpace(DatabasePath) || !File.Exists(DatabasePath))
        {
            SettingsStatusMessage = "Không tìm thấy database để sao lưu.";
            return;
        }

        DatabaseUpgrader.BackupDatabase(DatabasePath);
        SettingsStatusMessage = "Đã sao lưu database vào thư mục backups.";
    }

    public StationSettingsDto BuildStationDto() => new()
    {
        StationName = StationName,
        OwnerName = OwnerName,
        Address = Address,
        Phone = Phone,
        Email = Email,
        TaxCode = TaxCode,
        LogoPath = LogoPath,
        TicketFooterText = TicketFooterText
    };

    public TicketDocumentRenderOptions BuildPreviewOptions() => new()
    {
        ScaleSiteName = StationName,
        OwnerName = OwnerName,
        Address = Address,
        Phone = Phone,
        Email = Email,
        TicketFooterText = TicketFooterText
    };

    private CameraDeviceSettingsDto BuildCameraDto() => new()
    {
        CameraName = CameraName,
        IsEnabled = CameraEnabled,
        RtspUrl = RtspUrl,
        Username = CameraUsername,
        Password = CameraPassword,
        PreviewEnabled = CameraPreviewEnabled,
        AutoConnectionCheck = AutoConnectionCheck,
        SnapshotTimeoutSeconds = SnapshotTimeoutSeconds,
        PhotoRetentionDays = PhotoRetentionDays
    };
}
