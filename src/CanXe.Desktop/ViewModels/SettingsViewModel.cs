using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly StationSettingsService _settingsService;
    private readonly ICameraConnectionTester _cameraTester;
    private readonly IScaleConnectionTester _scaleTester;
    private readonly AppSettings _appSettings;
    private readonly IHardwareScaleDiagnostics? _hardwareScale;
    private readonly IBuildInfoProvider _buildInfoProvider;
    private readonly ISystemDiagnosticsService _diagnosticsService;

    public SettingsViewModel(
        StationSettingsService settingsService,
        ICameraConnectionTester cameraTester,
        IScaleConnectionTester scaleTester,
        AppSettings appSettings,
        IBuildInfoProvider buildInfoProvider,
        ISystemDiagnosticsService diagnosticsService,
        IHardwareScaleDiagnostics? hardwareScaleDiagnostics = null)
    {
        _settingsService = settingsService;
        _cameraTester = cameraTester;
        _scaleTester = scaleTester;
        _appSettings = appSettings;
        _buildInfoProvider = buildInfoProvider;
        _diagnosticsService = diagnosticsService;
        _hardwareScale = hardwareScaleDiagnostics;
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
    [ObservableProperty] private int _baudRate = 1200;
    [ObservableProperty] private int _dataBits = 8;
    [ObservableProperty] private string _parity = "None";
    [ObservableProperty] private string _stopBits = "One";
    [ObservableProperty] private string _handshake = "None";
    [ObservableProperty] private string? _scaleTestMessage;
    [ObservableProperty] private ScaleInputMode? _savedScaleInputMode;
    [ObservableProperty] private bool _autoConnectScaleOnStartup = true;

    [ObservableProperty] private string? _cameraName;
    [ObservableProperty] private bool _cameraEnabled = true;
    [ObservableProperty] private string? _rtspHost;
    [ObservableProperty] private int _rtspPort = 554;
    [ObservableProperty] private string? _rtspPath = "/";
    [ObservableProperty] private string? _cameraUsername;
    [ObservableProperty] private string? _cameraPassword;
    [ObservableProperty] private bool _hasStoredCameraPassword;
    [ObservableProperty] private bool _showCameraPassword;
    private bool _clearStoredPasswordPending;
    [ObservableProperty] private string _rtspTransport = "TCP";
    [ObservableProperty] private bool _cameraPreviewEnabled = true;
    [ObservableProperty] private bool _autoConnectCameraOnStartup = true;
    [ObservableProperty] private int _connectTimeoutSeconds = 5;
    [ObservableProperty] private int _snapshotTimeoutSeconds = 5;
    [ObservableProperty] private int _photoRetentionDays = 3;
    [ObservableProperty] private string? _rtspHostError;
    [ObservableProperty] private string? _photoRetentionError;
    [ObservableProperty] private string? _cameraTestMessage;
    [ObservableProperty] private string? _settingsStatusMessage;

    [ObservableProperty] private string _applicationVersion = "1.7.0";
    [ObservableProperty] private string _databasePath = string.Empty;
    [ObservableProperty] private string _deviceModeDisplay = "Simulation";
    [ObservableProperty] private string _migrationStatus = "—";
    [ObservableProperty] private string _errorLogSummary = "Không có lỗi gần đây.";
    [ObservableProperty] private string _buildInfoText = string.Empty;
    [ObservableProperty] private string _cameraHealthText = "Chưa có dữ liệu camera.";
    [ObservableProperty] private bool _isDiagnosticsRunning;
    [ObservableProperty] private string? _diagnosticsStatusMessage;
    [ObservableProperty] private string? _lastDiagnosticsReportPath;

    public ObservableCollection<DiagnosticRowViewModel> DiagnosticRows { get; } = [];

    public string CameraPasswordPlaceholder =>
        HasStoredCameraPassword && string.IsNullOrWhiteSpace(CameraPassword) ? "••••••" : string.Empty;

    public event EventHandler<StationSettingsDto>? StationSettingsSaved;
    public event EventHandler? CameraSettingsSaved;

    public async Task LoadAsync(string databasePath)
    {
        DatabasePath = databasePath;
        DeviceModeDisplay = _appSettings.DeviceMode;
        MigrationStatus = $"{DatabaseUpgrader.Phase16MigrationId}, {DatabaseUpgrader.Phase17MigrationId}, {DatabaseUpgrader.Phase2CMigrationId}, {DatabaseUpgrader.Phase2DMigrationId}, {DatabaseUpgrader.Phase3AMigrationId}";

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
        DeviceMode = !string.IsNullOrWhiteSpace(_appSettings.DeviceMode)
            ? _appSettings.DeviceMode
            : scale.DeviceMode;
        PortName = scale.PortName;
        BaudRate = scale.BaudRate;
        DataBits = scale.DataBits;
        Parity = scale.Parity;
        StopBits = scale.StopBits;
        Handshake = scale.Handshake;
        SavedScaleInputMode = scale.ScaleInputMode;
        AutoConnectScaleOnStartup = scale.AutoConnectScaleOnStartup;
        if (!ScaleInputModeDisplay.IsHardwareDeviceMode(DeviceMode))
            AutoConnectScaleOnStartup = false;

        var camera = await _settingsService.GetCameraAsync();
        CameraName = camera.CameraName;
        CameraEnabled = camera.IsEnabled;
        RtspHost = camera.RtspHost;
        RtspPort = camera.RtspPort > 0 ? camera.RtspPort : 554;
        RtspPath = string.IsNullOrWhiteSpace(camera.RtspPath) ? "/" : camera.RtspPath;
        CameraUsername = camera.Username;
        CameraPassword = null;
        HasStoredCameraPassword = camera.HasStoredPassword;
        CameraPreviewEnabled = camera.PreviewEnabled;
        AutoConnectCameraOnStartup = camera.AutoConnectCameraOnStartup;
        ConnectTimeoutSeconds = camera.ConnectTimeoutSeconds;
        SnapshotTimeoutSeconds = camera.SnapshotTimeoutSeconds;
        PhotoRetentionDays = camera.PhotoRetentionDays;
        RtspTransport = string.IsNullOrWhiteSpace(camera.RtspTransport) ? "TCP" : camera.RtspTransport;
        OnPropertyChanged(nameof(CameraPasswordPlaceholder));
        RefreshBuildInfo();
    }

    private void RefreshBuildInfo()
    {
        var info = _buildInfoProvider.GetBuildInfo();
        BuildInfoText = info.ToDisplayText();
        ApplicationVersion = info.Version;
        DeviceModeDisplay = info.DeviceMode;
    }

    public void RefreshCameraHealth(CameraHealthSnapshot snapshot, string? extendedText = null) =>
        CameraHealthText = extendedText ?? snapshot.ToDisplayText();

    [RelayCommand]
    private void CopyBuildInfo()
    {
        Clipboard.SetText(BuildInfoText);
        SettingsStatusMessage = "Đã sao chép thông tin phiên bản.";
    }

    [RelayCommand]
    private async Task RunFullDiagnosticsAsync()
    {
        if (IsDiagnosticsRunning)
            return;

        IsDiagnosticsRunning = true;
        DiagnosticsStatusMessage = "Đang chạy kiểm tra…";
        DiagnosticRows.Clear();

        try
        {
            var result = await _diagnosticsService.RunAllAsync(new DiagnosticsRunOptions
            {
                RequireCamera = false,
                RequireScale = false
            }).ConfigureAwait(true);

            foreach (var check in result.Checks)
            {
                DiagnosticRows.Add(new DiagnosticRowViewModel
                {
                    Category = check.Category,
                    Name = check.Name,
                    Status = check.Status.ToString().ToUpperInvariant(),
                    Detail = check.Detail,
                    DurationText = $"{check.Duration.TotalMilliseconds:F0} ms"
                });
            }

            LastDiagnosticsReportPath = await _diagnosticsService.ExportReportAsync(result).ConfigureAwait(true);
            DiagnosticsStatusMessage =
                $"Hoàn tất: {result.Passed} PASS, {result.Failed} FAIL, {result.Skipped} SKIP";
        }
        catch (Exception ex)
        {
            DiagnosticsStatusMessage = $"Lỗi kiểm tra: {ex.Message}";
        }
        finally
        {
            IsDiagnosticsRunning = false;
        }
    }

    [RelayCommand]
    private async Task ExportDiagnosticsReportAsync()
    {
        if (DiagnosticRows.Count == 0)
        {
            SettingsStatusMessage = "Chưa có kết quả kiểm tra để xuất.";
            return;
        }

        var checks = DiagnosticRows.Select(r => new SystemDiagnosticCheck
        {
            Category = r.Category,
            Name = r.Name,
            Status = Enum.TryParse<DiagnosticStatus>(r.Status, true, out var s) ? s : DiagnosticStatus.Warning,
            Detail = r.Detail,
            Duration = TimeSpan.FromMilliseconds(double.TryParse(r.DurationText.Replace(" ms", ""), out var ms) ? ms : 0)
        }).ToList();

        var result = new SystemDiagnosticsResult { Checks = checks };
        LastDiagnosticsReportPath = await _diagnosticsService.ExportReportAsync(result).ConfigureAwait(true);
        SettingsStatusMessage = $"Đã xuất báo cáo: {LastDiagnosticsReportPath}";
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
    private async Task SaveScaleAsync() =>
        await PersistScaleSettingsAsync(SavedScaleInputMode).ConfigureAwait(false);

    public async Task PersistScaleSettingsAsync(ScaleInputMode? activeMode)
    {
        SavedScaleInputMode = activeMode;
        await _settingsService.SaveScaleAsync(new ScaleDeviceSettingsDto
        {
            DeviceMode = DeviceMode,
            PortName = PortName,
            BaudRate = BaudRate,
            DataBits = DataBits,
            Parity = Parity,
            StopBits = StopBits,
            Handshake = Handshake,
            ScaleInputMode = activeMode,
            AutoConnectScaleOnStartup = AutoConnectScaleOnStartup
        }).ConfigureAwait(false);
    }

    public async Task SaveScaleInputModeAsync(ScaleInputMode mode)
    {
        await PersistScaleSettingsAsync(mode).ConfigureAwait(false);
    }

    public ScaleDeviceSettingsDto BuildScaleDto() => new()
    {
        DeviceMode = DeviceMode,
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        Parity = Parity,
        StopBits = StopBits,
        Handshake = Handshake,
        ScaleInputMode = SavedScaleInputMode,
        AutoConnectScaleOnStartup = AutoConnectScaleOnStartup
    };

    [RelayCommand]
    private async Task SaveCameraAsync()
    {
        RtspHostError = null;
        PhotoRetentionError = null;
        var dto = BuildCameraDto();
        var (success, validation) = await _settingsService.SaveCameraAsync(dto);
        if (!success)
        {
            RtspHostError = validation.Errors.GetValueOrDefault(nameof(CameraDeviceSettingsDto.RtspHost));
            PhotoRetentionError = validation.Errors.GetValueOrDefault(nameof(CameraDeviceSettingsDto.PhotoRetentionDays));
            SettingsStatusMessage = "Không lưu được cấu hình camera.";
            return;
        }

        HasStoredCameraPassword = dto.ClearStoredPassword ? false : HasStoredCameraPassword || !string.IsNullOrEmpty(dto.Password);
        _clearStoredPasswordPending = false;
        CameraPassword = null;
        OnPropertyChanged(nameof(CameraPasswordPlaceholder));
        SettingsStatusMessage = "Đã lưu cấu hình camera.";
        CameraSettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearStoredCameraPassword()
    {
        CameraPassword = null;
        HasStoredCameraPassword = false;
        _clearStoredPasswordPending = true;
        OnPropertyChanged(nameof(CameraPasswordPlaceholder));
        SettingsStatusMessage = "Mật khẩu đã lưu sẽ bị xóa khi bấm LƯU CẤU HÌNH.";
    }

    [RelayCommand]
    private async Task TestRtspAsync()
    {
        CameraTestMessage = null;
        var runtime = await BuildCameraRuntimeForTestAsync();
        var dto = CameraSettingsMapper.FromRuntime(runtime);
        dto.Password = runtime.Password;
        var result = await _cameraTester.TestAsync(dto);
        CameraTestMessage = result.Message;
    }

    [RelayCommand]
    private async Task TestScaleAsync()
    {
        ScaleTestMessage = null;
        var result = await _scaleTester.TestAsync(BuildScaleDto());
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

    public CameraDeviceSettingsDto BuildCameraDto() => new()
    {
        CameraName = CameraName,
        IsEnabled = CameraEnabled,
        RtspHost = RtspHost,
        RtspPort = RtspPort,
        RtspPath = RtspPath,
        Username = CameraUsername,
        Password = CameraPassword,
        ClearStoredPassword = _clearStoredPasswordPending,
        RtspTransport = RtspTransport,
        PreviewEnabled = CameraPreviewEnabled,
        AutoConnectCameraOnStartup = AutoConnectCameraOnStartup,
        ConnectTimeoutSeconds = ConnectTimeoutSeconds,
        SnapshotTimeoutSeconds = SnapshotTimeoutSeconds,
        PhotoRetentionDays = PhotoRetentionDays
    };

    private async Task<CameraRuntimeSettings> BuildCameraRuntimeForTestAsync()
    {
        var dto = BuildCameraDto();
        if (string.IsNullOrEmpty(dto.Password) && HasStoredCameraPassword)
        {
            var stored = await _settingsService.GetCameraRuntimeAsync();
            return CameraSettingsMapper.ToRuntime(dto, stored.Password);
        }

        return CameraSettingsMapper.ToRuntime(dto, dto.Password);
    }
}
