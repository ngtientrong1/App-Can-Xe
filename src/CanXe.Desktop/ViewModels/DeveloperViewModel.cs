using System.Diagnostics;
using System.IO;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.ScaleProtocol.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanXe.Desktop.ViewModels;

public sealed partial class DeveloperViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly AppPaths _paths;
    private readonly IBuildInfoProvider _buildInfo;
    private readonly ISystemDiagnosticsService _diagnostics;
    private readonly IHardwareScaleDiagnostics? _hardwareScale;
    private readonly PrintSettingsService _printSettings;

    public DeveloperViewModel(
        AppSettings settings,
        AppPaths paths,
        IBuildInfoProvider buildInfo,
        ISystemDiagnosticsService diagnostics,
        PrintSettingsService printSettings,
        IHardwareScaleDiagnostics? hardwareScale = null)
    {
        _settings = settings;
        _paths = paths;
        _buildInfo = buildInfo;
        _diagnostics = diagnostics;
        _printSettings = printSettings;
        _hardwareScale = hardwareScale;
        RefreshBuildInfo();
        RefreshScaleStatus();
    }

    [ObservableProperty] private string _buildInfoText = string.Empty;
    [ObservableProperty] private string _scaleStatusText = string.Empty;
    [ObservableProperty] private string _diagnosticsSummary = string.Empty;
    [ObservableProperty] private string _lastStartupError = "—";
    [ObservableProperty] private string _lastScaleError = "—";
    [ObservableProperty] private string _lastDatabaseError = "—";
    [ObservableProperty] private string _lastPrinterError = "—";
    [ObservableProperty] private bool _simulateScaleStable = true;
    [ObservableProperty] private bool _simulateScaleDisconnected;
    [ObservableProperty] private string _simulatedWeightText = "18500";

    public bool IsSimulationVisible => _settings.DeveloperMode;

    [RelayCommand]
    private void CopyBuildInfo()
    {
        ClipboardHelper.SetText(BuildInfoText);
    }

    [RelayCommand]
    private void OpenAppDirectory() => Process.Start(new ProcessStartInfo(AppContext.BaseDirectory) { UseShellExecute = true });

    [RelayCommand]
    private void OpenLogDirectory()
    {
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanXe", "Logs");
        Directory.CreateDirectory(logDir);
        Process.Start(new ProcessStartInfo(logDir) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task RunFullDiagnosticsAsync()
    {
        var result = await _diagnostics.RunAllAsync(new DiagnosticsRunOptions
        {
            IncludeApplication = true,
            IncludeDatabase = true,
            IncludeScale = true,
            IncludePrinter = true,
            IncludeFilesystem = true
        }).ConfigureAwait(true);
        DiagnosticsSummary = $"{result.Passed} pass, {result.Failed} fail";
    }

    [RelayCommand]
    private async Task ExportDiagnosticsReportAsync()
    {
        var result = await _diagnostics.RunAllAsync(new DiagnosticsRunOptions
        {
            IncludeApplication = true,
            IncludeDatabase = true,
            IncludeScale = true,
            IncludePrinter = true,
            IncludeFilesystem = true
        }).ConfigureAwait(true);
        var path = await _diagnostics.ExportReportAsync(result).ConfigureAwait(true);
        DiagnosticsSummary = $"Report: {path}";
    }

    public void RefreshBuildInfo()
    {
        var info = _buildInfo.GetBuildInfo();
        BuildInfoText =
            $"Version: {info.Version}\r\n" +
            $"Build: {info.BuildTimestamp}\r\n" +
            $"Commit: {info.GitCommit}\r\n" +
            $"Configuration: {info.Configuration}\r\n" +
            $"DeviceMode: {_settings.DeviceMode}\r\n" +
            $"App: {AppContext.BaseDirectory}\r\n" +
            $"Database: {_paths.DatabasePath}\r\n" +
            $"Logs: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanXe", "Logs")}";
    }

    public void RefreshScaleStatus()
    {
        if (_hardwareScale is null)
        {
            ScaleStatusText = "Hardware scale service unavailable.";
            return;
        }

        var reading = _hardwareScale.LatestReading;
        ScaleStatusText =
            $"COM: {_hardwareScale.HardwareSettings.PortName} @ {_hardwareScale.HardwareSettings.BaudRate}\r\n" +
            $"Connected: {_hardwareScale.IsConnected}\r\n" +
            $"Stable: {reading?.IsStable} ({reading?.StableSource})\r\n" +
            $"Weight: {reading?.WeightKg:N0} kg\r\n" +
            $"Valid frames: {_hardwareScale.ValidFrameCount}\r\n" +
            $"Invalid frames: {_hardwareScale.InvalidFrameCount}";
    }
}

internal static class ClipboardHelper
{
    public static void SetText(string text) =>
        System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Clipboard.SetText(text));
}
