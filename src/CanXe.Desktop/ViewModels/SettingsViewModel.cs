using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Infrastructure.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

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
    private readonly IBackupService _backupService;
    private readonly IUserPermissionService _permissions;
    private readonly IAdminAuthorizationService _adminAuthorization;
    private readonly IPrintNotificationService _notifications;
    private readonly IThemeService _themeService;
    private bool _suppressAutoBackupPersist;
    private bool _suppressThemePersist;

    public SettingsViewModel(
        StationSettingsService settingsService,
        IScaleConnectionTester scaleTester,
        IPrinterCapabilityService printerCapability,
        AppSettings appSettings,
        IBuildInfoProvider buildInfoProvider,
        ISystemDiagnosticsService diagnosticsService,
        IBackupService backupService,
        IUserPermissionService permissions,
        IAdminAuthorizationService adminAuthorization,
        IPrintNotificationService notifications,
        IThemeService themeService,
        IHardwareScaleDiagnostics? hardwareScaleDiagnostics = null)
    {
        _settingsService = settingsService;
        _scaleTester = scaleTester;
        _printerCapability = printerCapability;
        _appSettings = appSettings;
        _buildInfoProvider = buildInfoProvider;
        _diagnosticsService = diagnosticsService;
        _backupService = backupService;
        _permissions = permissions;
        _adminAuthorization = adminAuthorization;
        _notifications = notifications;
        _themeService = themeService;
        _hardwareScale = hardwareScaleDiagnostics;
        _adminAuthorization.AdminSessionChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshBackupAdminState);
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

    [ObservableProperty] private string _backupFolderPath = string.Empty;
    [ObservableProperty] private string _latestBackupDisplay = "Chưa có";
    [ObservableProperty] private bool _autoBackupEnabled;
    [ObservableProperty] private bool _retentionDays15;
    [ObservableProperty] private bool _retentionDays30 = true;
    [ObservableProperty] private bool _isBackupBusy;
    [ObservableProperty] private bool _isBackupAdminUnlocked;
    [ObservableProperty] private string? _backupStatusMessage;
    [ObservableProperty] private string? _settingsStatusMessage;

    [ObservableProperty] private AppThemeMode _themeMode = AppThemeMode.Light;

    public string ThemeAutoHint =>
        "Tự động: dùng giao diện tối từ 18:00 đến 05:59, giao diện sáng từ 06:00 đến 17:59.";

    public bool IsThemeLightSelected => ThemeMode == AppThemeMode.Light;
    public bool IsThemeDarkSelected => ThemeMode == AppThemeMode.Dark;
    public bool IsThemeAutoSelected => ThemeMode == AppThemeMode.Auto;

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
        RefreshBackupUiFromStore();
        SyncThemeFromService();
    }

    public void SyncThemeFromService()
    {
        _suppressThemePersist = true;
        ThemeMode = _themeService.Settings.ThemeMode;
        _suppressThemePersist = false;
    }

    partial void OnThemeModeChanged(AppThemeMode value)
    {
        if (!_suppressThemePersist)
            _themeService.ApplyThemeMode(value);

        OnPropertyChanged(nameof(IsThemeLightSelected));
        OnPropertyChanged(nameof(IsThemeDarkSelected));
        OnPropertyChanged(nameof(IsThemeAutoSelected));
    }

    [RelayCommand]
    private void SelectThemeLight() => ThemeMode = AppThemeMode.Light;

    [RelayCommand]
    private void SelectThemeDark() => ThemeMode = AppThemeMode.Dark;

    [RelayCommand]
    private void SelectThemeAuto() => ThemeMode = AppThemeMode.Auto;

    public void RefreshBackupUiFromStore()
    {
        var settings = _backupService.LoadUserSettings();
        _suppressAutoBackupPersist = true;
        BackupFolderPath = settings.BackupFolderPath ?? string.Empty;
        AutoBackupEnabled = settings.AutoBackupEnabled;
        _suppressAutoBackupPersist = false;
        var days = settings.RetentionDays is 15 or 30 ? settings.RetentionDays : 30;
        RetentionDays15 = days == 15;
        RetentionDays30 = days == 30;
        RefreshLatestBackupDisplay();
        RefreshBackupAdminState();
    }

    public void RefreshBackupAdminState() =>
        IsBackupAdminUnlocked = _adminAuthorization.IsAdminUnlocked;

    private void RefreshLatestBackupDisplay()
    {
        var latest = _backupService.GetLatestBackupTime(
            string.IsNullOrWhiteSpace(BackupFolderPath) ? null : BackupFolderPath);
        LatestBackupDisplay = latest is null
            ? "Chưa có"
            : latest.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
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

    public async Task SaveScaleInputModeAsync(ScaleInputMode mode) =>
        await PersistScaleSettingsAsync(mode).ConfigureAwait(false);

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
    private async Task CreateBackupNowAsync()
    {
        if (IsBackupBusy)
            return;

        if (string.IsNullOrWhiteSpace(BackupFolderPath))
        {
            BackupStatusMessage = "Chưa chọn thư mục backup.";
            MessageBox.Show(
                "Vui lòng chọn thư mục backup trước.",
                "CanXe",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        IsBackupBusy = true;
        BackupStatusMessage = "Đang tạo backup…";
        try
        {
            PersistBackupSettings();
            var result = await _backupService.CreateBackupAsync(StationName).ConfigureAwait(true);
            if (!result.Success)
            {
                BackupStatusMessage = result.ErrorMessage ?? "Không thể tạo backup.";
                MessageBox.Show(
                    result.ErrorMessage ?? "Không thể tạo backup. Vui lòng kiểm tra thư mục backup.",
                    "CanXe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            RefreshLatestBackupDisplay();
            BackupStatusMessage = "Đã tạo backup.";
            _ = _notifications.ShowCatalogDeleteSuccessToastAsync("Đã tạo backup");
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    [RelayCommand]
    private void ChooseBackupFolder()
    {
        if (!EnsureBackupAdmin(AdminPermission.CanManageBackup, "BACKUP_FOLDER"))
            return;

        var dialog = new OpenFolderDialog { Title = "Chọn thư mục backup" };
        if (!string.IsNullOrWhiteSpace(BackupFolderPath) && Directory.Exists(BackupFolderPath))
            dialog.InitialDirectory = BackupFolderPath;

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            return;

        BackupFolderPath = dialog.FolderName;
        PersistBackupSettings();
        RefreshLatestBackupDisplay();
        BackupStatusMessage = "Đã cập nhật thư mục backup.";
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        if (string.IsNullOrWhiteSpace(BackupFolderPath))
        {
            MessageBox.Show("Chưa chọn thư mục backup.", "CanXe", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(BackupFolderPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = BackupFolderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            BackupLogger.Write("OPEN_BACKUP_FOLDER_FAILED", ex.GetType().Name);
            MessageBox.Show("Không thể mở thư mục backup.", "CanXe", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task RestoreFromBackupAsync()
    {
        if (!EnsureBackupAdmin(AdminPermission.CanRestoreBackup, "RESTORE"))
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Chọn file backup CanXe",
            Filter = "CanXe backup (*.canxebackup)|*.canxebackup|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (!string.IsNullOrWhiteSpace(BackupFolderPath) && Directory.Exists(BackupFolderPath))
            dialog.InitialDirectory = BackupFolderPath;

        if (dialog.ShowDialog() != true)
            return;

        var confirm = MessageBox.Show(
            "Khôi phục backup sẽ thay thế dữ liệu hiện tại. Ứng dụng sẽ tự tạo một bản backup hiện tại trước khi khôi phục. Bạn có muốn tiếp tục?",
            "Xác nhận khôi phục",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        IsBackupBusy = true;
        BackupStatusMessage = "Đang khôi phục…";
        try
        {
            PersistBackupSettings();
            var result = await _backupService.RestoreBackupAsync(dialog.FileName).ConfigureAwait(true);
            if (!result.Success)
            {
                BackupStatusMessage = result.ErrorMessage ?? "Khôi phục thất bại.";
                MessageBox.Show(
                    result.ErrorMessage ?? "Không thể khôi phục backup.",
                    "CanXe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                "Khôi phục thành công. Vui lòng khởi động lại ứng dụng.",
                "CanXe",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            System.Windows.Application.Current?.Shutdown();
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    [RelayCommand]
    private void SetRetention15()
    {
        if (!EnsureBackupAdmin(AdminPermission.CanManageBackup, "BACKUP_RETENTION"))
        {
            RefreshBackupUiFromStore();
            return;
        }

        RetentionDays15 = true;
        RetentionDays30 = false;
        PersistBackupSettings();
    }

    [RelayCommand]
    private void SetRetention30()
    {
        if (!EnsureBackupAdmin(AdminPermission.CanManageBackup, "BACKUP_RETENTION"))
        {
            RefreshBackupUiFromStore();
            return;
        }

        RetentionDays15 = false;
        RetentionDays30 = true;
        PersistBackupSettings();
    }

    partial void OnAutoBackupEnabledChanged(bool value)
    {
        if (_suppressAutoBackupPersist)
            return;

        if (!IsBackupAdminUnlocked)
        {
            _suppressAutoBackupPersist = true;
            AutoBackupEnabled = _backupService.LoadUserSettings().AutoBackupEnabled;
            _suppressAutoBackupPersist = false;
            EnsureBackupAdmin(AdminPermission.CanManageBackup, "AUTO_BACKUP");
            return;
        }

        PersistBackupSettings();
    }

    public async Task RunAutoBackupIfNeededAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = _backupService.LoadUserSettings();
            if (!settings.AutoBackupEnabled)
            {
                BackupLogger.Write("AUTO_BACKUP_SKIPPED", "disabled");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.BackupFolderPath))
            {
                BackupLogger.Write("AUTO_BACKUP_SKIPPED", "folder-not-set");
                return;
            }

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (string.Equals(settings.LastAutoBackupDate, today, StringComparison.Ordinal))
            {
                BackupLogger.Write("AUTO_BACKUP_SKIPPED", "already-today");
                return;
            }

            var result = await _backupService.CreateBackupAsync(StationName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success)
            {
                BackupLogger.Write("AUTO_BACKUP_FAILED", result.ErrorMessage ?? "unknown");
                return;
            }

            settings = _backupService.LoadUserSettings();
            settings.LastAutoBackupDate = today;
            settings.LastBackupAt = DateTimeOffset.Now.ToString("O");
            _backupService.SaveUserSettings(settings);
            _backupService.CleanupRetention(settings.BackupFolderPath, settings.RetentionDays);
            BackupLogger.Write("AUTO_BACKUP_COMPLETED", Path.GetFileName(result.FilePath ?? string.Empty));

            await RunOnUiAsync(RefreshBackupUiFromStore).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            BackupLogger.Write("AUTO_BACKUP_FAILED", ex.GetType().Name);
        }
    }

    private void PersistBackupSettings()
    {
        var settings = _backupService.LoadUserSettings();
        settings.BackupFolderPath = string.IsNullOrWhiteSpace(BackupFolderPath) ? null : BackupFolderPath.Trim();
        settings.AutoBackupEnabled = AutoBackupEnabled;
        settings.RetentionDays = RetentionDays15 ? 15 : 30;
        _backupService.SaveUserSettings(settings);
        if (!string.IsNullOrWhiteSpace(settings.BackupFolderPath))
            _backupService.CleanupRetention(settings.BackupFolderPath, settings.RetentionDays);
    }

    private bool EnsureBackupAdmin(AdminPermission permission, string action)
    {
        var gate = _permissions.EnsurePermission(permission, action);
        if (gate.Allowed)
        {
            RefreshBackupAdminState();
            return true;
        }

        MessageBox.Show(
            gate.ErrorMessage ?? "Cần mở khóa Admin để thực hiện thao tác này.",
            "CanXe",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
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
