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
    private readonly IScaleConnectionTester _scaleTester;
    private readonly IPrinterCapabilityService _printerCapability;
    private readonly AppSettings _appSettings;
    private readonly IHardwareScaleDiagnostics? _hardwareScale;
    private readonly IBuildInfoProvider _buildInfoProvider;
    private readonly ISystemDiagnosticsService _diagnosticsService;

    public SettingsViewModel(
        StationSettingsService settingsService,
        IScaleConnectionTester scaleTester,
        IPrinterCapabilityService printerCapability,
        AppSettings appSettings,
        IBuildInfoProvider buildInfoProvider,
        ISystemDiagnosticsService diagnosticsService,
        IHardwareScaleDiagnostics? hardwareScaleDiagnostics = null)
    {
        _settingsService = settingsService;
        _scaleTester = scaleTester;
        _printerCapability = printerCapability;
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

    [ObservableProperty] private string _defaultPrinterName = "—";
    [ObservableProperty] private string _printerStatusText = "—";
    [ObservableProperty] private string? _printerTestMessage;

    [ObservableProperty] private string _applicationVersion = "1.7.0";
    [ObservableProperty] private string _databasePath = string.Empty;
    [ObservableProperty] private string _deviceModeDisplay = "Simulation";
    [ObservableProperty] private string _migrationStatus = "—";
    [ObservableProperty] private string _errorLogSummary = "Không có lỗi gần đây.";
    [ObservableProperty] private string _buildInfoText = string.Empty;
    [ObservableProperty] private bool _isDiagnosticsRunning;
    [ObservableProperty] private string? _diagnosticsStatusMessage;
    [ObservableProperty] private string? _lastDiagnosticsReportPath;

    public ObservableCollection<DiagnosticRowViewModel> DiagnosticRows { get; } = [];

    public event EventHandler<StationSettingsDto>? StationSettingsSaved;

    public async Task LoadAsync(string databasePath)
    {
        DatabasePath = databasePath;
        DeviceModeDisplay = _appSettings.DeviceMode;
        MigrationStatus =
            $"{DatabaseUpgrader.Phase16MigrationId}, {DatabaseUpgrader.Phase17MigrationId}, " +
            $"{DatabaseUpgrader.Phase2CMigrationId}, {DatabaseUpgrader.Phase2DMigrationId}, " +
            $"{DatabaseUpgrader.Phase3AMigrationId}, {DatabaseUpgrader.Phase4MigrationId}";

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

        RefreshPrinterInfo();
        RefreshBuildInfo();
    }

    private void RefreshPrinterInfo()
    {
        var printer = _printerCapability.GetDefaultPrinter();
        DefaultPrinterName = printer?.Name ?? "Chưa cấu hình";
        PrinterStatusText = printer is null
            ? "Không tìm thấy máy in mặc định."
            : $"A4: {(printer.SupportsA4 ? "Có" : "Không")}";
    }

    private void RefreshBuildInfo()
    {
        var info = _buildInfoProvider.GetBuildInfo();
        BuildInfoText = info.ToDisplayText();
        ApplicationVersion = info.Version;
        DeviceModeDisplay = info.DeviceMode;
    }

    [RelayCommand]
    private void CopyBuildInfo()
    {
        Clipboard.SetText(BuildInfoText);
        SettingsStatusMessage = "Đã sao chép thông tin phiên bản.";
    }

    [ObservableProperty] private string? _settingsStatusMessage;

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
                IncludeApplication = true,
                IncludeDatabase = true,
                IncludeFilesystem = true,
                IncludePrinter = true,
                IncludeScale = true,
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
    private async Task TestScaleAsync()
    {
        ScaleTestMessage = null;
        var result = await _scaleTester.TestAsync(BuildScaleDto());
        ScaleTestMessage = result.Message;
    }

    [RelayCommand]
    private void TestPrint()
    {
        PrinterTestMessage = null;
        var validation = _printerCapability.ValidatePrinter(null);
        PrinterTestMessage = validation.Success
            ? $"Máy in sẵn sàng: {validation.Printer?.Name}"
            : validation.ErrorMessage ?? "Máy in không khả dụng.";
        RefreshPrinterInfo();
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
}
