using System.Diagnostics;
using System.Text.Json;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class DiagnosticsPhaseRunner(
    AppSettings appSettings,
    AppPaths appPaths,
    IBuildInfoProvider buildInfoProvider,
    CanXeDbContext dbContext,
    StationSettingsService settingsService)
{
    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunPublishChecksAsync(
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

        checks.Add(await RunCheckAsync("Publish", "Production mode valid", async () =>
        {
            if (!File.Exists(configPath))
                return Fail("appsettings.json missing");

            var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var deviceMode = root.TryGetProperty("DeviceMode", out var dm) ? dm.GetString() : null;
            var developerMode = root.TryGetProperty("DeveloperMode", out var dev) && dev.GetBoolean();
            var showDev = root.TryGetProperty("ShowDeveloperPanel", out var sdp) && sdp.GetBoolean();

            if (!ProductionConfigPolicy.IsValidProductionConfig(deviceMode, developerMode, showDev, json))
                return Fail($"Invalid production config (mode={deviceMode}, dev={developerMode})");

            return Pass("Hardware mode, DeveloperMode=false");
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Publish", "No bundled FFmpeg", () =>
            Task.FromResult(File.Exists(ffmpegPath)
                ? Fail("ffmpeg/ffmpeg.exe must not be packaged in Phase 4 production")
                : Pass("ffmpeg not present")),
            cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Publish", "No camera config keys", async () =>
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            if (json.Contains("CameraPreviewEnabled", StringComparison.OrdinalIgnoreCase)
                || json.Contains("RtspHost", StringComparison.OrdinalIgnoreCase))
                return Fail("Production appsettings still contains camera keys");

            return Pass("Camera config absent");
        }, cancellationToken).ConfigureAwait(false));

        return checks;
    }

    public Task<IReadOnlyList<SystemDiagnosticCheck>> RunApplicationChecksAsync(CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();
        var info = buildInfoProvider.GetBuildInfo();
        checks.Add(RunCheckSync("Application", "Build info", Pass($"{info.Version} @ {info.BuildTimestamp}")));
        checks.Add(RunCheckSync("Application", "Device mode",
            ProductionConfigPolicy.IsHardwareMode(appSettings.DeviceMode)
                ? Pass(appSettings.DeviceMode)
                : Fail($"Expected Hardware, got {appSettings.DeviceMode}")));
        return Task.FromResult<IReadOnlyList<SystemDiagnosticCheck>>(checks);
    }

    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunDatabaseChecksAsync(CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();

        checks.Add(await RunCheckAsync("Database", "SQLite connect", async () =>
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            return canConnect ? Pass(appPaths.DatabasePath) : Fail("Cannot connect");
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Database", "Station settings row", async () =>
        {
            var station = await settingsService.GetStationAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(station.StationName)
                ? Fail("Station name empty")
                : Pass(station.StationName);
        }, cancellationToken).ConfigureAwait(false));

        return checks;
    }

    public Task<IReadOnlyList<SystemDiagnosticCheck>> RunFilesystemChecksAsync(CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();
        var dir = Path.GetDirectoryName(appPaths.DatabasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        Directory.CreateDirectory(appPaths.PhotoRoot);
        checks.Add(RunCheckSync("Filesystem", "Database path writable", Pass(dir ?? appPaths.DatabasePath)));
        checks.Add(RunCheckSync("Filesystem", "Photo root exists", Pass(appPaths.PhotoRoot)));
        return Task.FromResult<IReadOnlyList<SystemDiagnosticCheck>>(checks);
    }

    public Task<IReadOnlyList<SystemDiagnosticCheck>> RunPrinterChecksAsync(CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>
        {
            RunCheckSync("Printer", "Default printer", Skip("Printer validation runs in desktop app"))
        };
        return Task.FromResult<IReadOnlyList<SystemDiagnosticCheck>>(checks);
    }

    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunScaleChecksAsync(
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();
        if (options.SkipScale)
            return checks;

        if (!ProductionConfigPolicy.IsHardwareMode(appSettings.DeviceMode))
        {
            checks.Add(RunCheckSync("Scale", "COM port exists",
                options.RequireScale ? Fail("Simulation mode") : Skip("Simulation mode")));
            return checks;
        }

        var scale = await settingsService.GetScaleAsync(cancellationToken).ConfigureAwait(false);
        var ports = System.IO.Ports.SerialPort.GetPortNames();
        var serialSettings = ScaleHardwareProbe.ToSerialSettings(scale);

        checks.Add(RunCheckSync("Scale", "COM port exists",
            ports.Length == 0
                ? (options.RequireScale ? Fail("No COM ports") : Skip("NOT AVAILABLE"))
                : ports.Contains(scale.PortName, StringComparer.OrdinalIgnoreCase)
                    ? Pass(scale.PortName)
                    : options.RequireScale ? Fail($"{scale.PortName} not found") : Skip("Port not present")));

        checks.Add(await RunCheckAsync("Scale", "Valid frame in timeout", async () =>
        {
            if (!ports.Contains(scale.PortName, StringComparer.OrdinalIgnoreCase))
                return options.RequireScale ? Fail("Port not available") : Skip("Port not available");

            var probe = await ScaleHardwareProbe.ReadValidFrameAsync(
                serialSettings,
                DiagnosticsTimeouts.Scale,
                cancellationToken).ConfigureAwait(false);

            if (probe.ValidFrames > 0 && probe.LastValidWeightKg is not null)
                return Pass($"weight={probe.LastValidWeightKg:N0} kg");

            return options.RequireScale
                ? Fail(probe.Error ?? "No valid frame")
                : Skip(probe.Error ?? "No valid frame");
        }, cancellationToken).ConfigureAwait(false));

        return checks;
    }

    private static SystemDiagnosticCheck RunCheckSync(
        string category,
        string name,
        (DiagnosticStatus Status, string Detail) result) =>
        new()
        {
            Category = category,
            Name = name,
            Status = result.Status,
            Detail = result.Detail
        };

    private static async Task<SystemDiagnosticCheck> RunCheckAsync(
        string category,
        string name,
        Func<Task<(DiagnosticStatus Status, string Detail)>> execute,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (status, detail) = await execute().ConfigureAwait(false);
            return new SystemDiagnosticCheck { Category = category, Name = name, Status = status, Detail = detail, Duration = sw.Elapsed };
        }
        catch (Exception ex)
        {
            return new SystemDiagnosticCheck
            {
                Category = category,
                Name = name,
                Status = DiagnosticStatus.Fail,
                Detail = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }

    private static (DiagnosticStatus, string) Pass(string detail) => (DiagnosticStatus.Pass, detail);
    private static (DiagnosticStatus, string) Fail(string detail) => (DiagnosticStatus.Fail, detail);
    private static (DiagnosticStatus, string) Skip(string detail) => (DiagnosticStatus.Skipped, detail);
}
