using System.Diagnostics;
using System.Text.Json;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;
using CanXe.Infrastructure.Data;
using CanXe.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class DiagnosticsPhaseRunner(
    AppSettings appSettings,
    AppPaths appPaths,
    IBuildInfoProvider buildInfoProvider,
    ICameraDecoderFactory decoderFactory,
    CanXeDbContext dbContext,
    StationSettingsService settingsService,
    ISecretProtector secretProtector)
{
    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunPublishChecksAsync(
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

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

        checks.Add(await RunCheckAsync("Publish", "Decoder guard", () =>
        {
            try
            {
                var decoder = decoderFactory.CreateDecoder();
                CameraDecoderGuard.EnsureValid(appSettings.DeviceMode, decoder);
                return Task.FromResult(Pass(decoder.GetType().Name));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Fail(ex.Message));
            }
        }, cancellationToken).ConfigureAwait(false));

        var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        checks.Add(await RunCheckAsync("Publish", "Bundled FFmpeg", () =>
            File.Exists(ffmpegPath)
                ? Task.FromResult(Pass(ffmpegPath))
                : Task.FromResult(Fail("ffmpeg/ffmpeg.exe missing")),
            cancellationToken).ConfigureAwait(false));

        if (File.Exists(ffmpegPath) && !string.IsNullOrWhiteSpace(options.TestMediaPath) && File.Exists(options.TestMediaPath))
        {
            checks.Add(await RunCheckAsync("Publish", "Snapshot from test media", async () =>
            {
                var before = FfmpegCapabilityProbe.CountFfmpegProcesses();
                var pipeline = await FfmpegCapabilityProbe.RunFilePipelineAsync(
                    ffmpegPath,
                    options.TestMediaPath!,
                    TimeSpan.FromSeconds(30),
                    cancellationToken).ConfigureAwait(false);

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                var after = FfmpegCapabilityProbe.CountFfmpegProcesses();

                if (!pipeline.HasFrames)
                    return Fail("No JPEG frames from test media");

                var jpeg = pipeline.Frames[0];
                var (w, h) = SnapshotImageAnalyzer.TryReadJpegDimensions(jpeg);
                var looksLikeJpeg = CameraSnapshotPolicy.LooksLikeJpeg(jpeg);
                var validation = ProductionSnapshotValidator.Validate(
                    jpeg.Length,
                    w,
                    h,
                    decodable: looksLikeJpeg && w > 0 && h > 0,
                    looksLikeJpeg: looksLikeJpeg,
                    isSolidColor: SnapshotImageAnalyzer.LooksLikeSolidColor(jpeg));

                if (ProductionSnapshotValidator.IsKnownPlaceholder(jpeg))
                    return Fail("Known placeholder snapshot");

                if (validation.IsHardFail)
                    return Fail(validation.Message);

                if (after > before)
                    return Fail($"FFmpeg process leak ({after - before} remaining)");

                return validation.Severity == SnapshotValidationSeverity.Warning
                    ? Warn($"{validation.Message}; {jpeg.Length / 1024} KB, {w}x{h}")
                    : Pass($"{validation.Message}; {w}x{h}");
            }, cancellationToken).ConfigureAwait(false));
        }

        return checks;
    }

    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunApplicationChecksAsync(CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();

        checks.Add(await RunCheckAsync("Application", "Production mode valid", () =>
        {
            if (ProductionConfigPolicy.IsHardwareMode(appSettings.DeviceMode))
                return Task.FromResult(Pass("Hardware"));
            if (ProductionConfigPolicy.IsSimulationMode(appSettings.DeviceMode))
                return Task.FromResult(Warn("Simulation (dev)"));
            return Task.FromResult(Pass(appSettings.DeviceMode));
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Application", "Build info readable", () =>
        {
            var info = buildInfoProvider.GetBuildInfo();
            return string.IsNullOrWhiteSpace(info.Version)
                ? Task.FromResult(Fail("Version missing"))
                : Task.FromResult(Pass(info.Version));
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Application", "App folder exists", () =>
            Task.FromResult(Directory.Exists(AppContext.BaseDirectory)
                ? Pass(AppContext.BaseDirectory)
                : Fail("Missing")), cancellationToken).ConfigureAwait(false));

        foreach (var (name, path) in new[]
        {
            ("Logs folder writable", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanXe", "Logs")),
            ("Photos folder writable", appPaths.PhotoRoot),
            ("Temp folder writable", Path.GetTempPath())
        })
        {
            checks.Add(await RunCheckAsync("Application", name, () =>
            {
                try
                {
                    Directory.CreateDirectory(path);
                    var testFile = Path.Combine(path, $".canxe-write-test-{Guid.NewGuid():N}");
                    File.WriteAllText(testFile, "ok");
                    File.Delete(testFile);
                    return Task.FromResult(Pass(path));
                }
                catch (Exception ex)
                {
                    return Task.FromResult(Fail(ex.Message));
                }
            }, cancellationToken).ConfigureAwait(false));
        }

        return checks;
    }

    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunDatabaseChecksAsync(CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();

        checks.Add(await RunCheckAsync("Database", "Database exists", () =>
            Task.FromResult(File.Exists(appPaths.DatabasePath)
                ? Pass(appPaths.DatabasePath)
                : Fail("Not found")), cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Database", "SQLite open", async () =>
        {
            try
            {
                await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
                return Pass("Connected");
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Database", "Read settings", async () =>
        {
            try
            {
                var station = await settingsService.GetStationAsync(cancellationToken).ConfigureAwait(false);
                return Pass(station.StationName);
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Database", "Transaction rollback", async () =>
        {
            var countBefore = await dbContext.Customers.CountAsync(cancellationToken).ConfigureAwait(false);
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken).ConfigureAwait(false);
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Fail(ex.Message);
            }

            dbContext.ChangeTracker.Clear();
            var countAfter = await dbContext.Customers.CountAsync(cancellationToken).ConfigureAwait(false);
            return countAfter == countBefore
                ? Pass($"Rollback OK (customers={countBefore})")
                : Fail($"Customer count changed {countBefore}->{countAfter}");
        }, cancellationToken).ConfigureAwait(false));

        return checks;
    }

    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunFfmpegChecksAsync(
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();
        var ffmpegPath = FfmpegPathResolver.ResolveFfmpegExecutable();

        checks.Add(await RunCheckAsync("FFmpeg", "ffmpeg.exe exists", () =>
            Task.FromResult(ffmpegPath is not null && File.Exists(ffmpegPath)
                ? Pass(ffmpegPath)
                : Fail("Not found")), cancellationToken).ConfigureAwait(false));

        if (ffmpegPath is null || !File.Exists(ffmpegPath))
            return checks;

        checks.Add(await RunCheckAsync("FFmpeg", "Version command", () =>
        {
            var version = FfmpegCapabilityProbe.GetVersion(ffmpegPath);
            return string.IsNullOrWhiteSpace(version)
                ? Task.FromResult(Fail("No version output"))
                : Task.FromResult(Pass(version));
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("FFmpeg", "No -stimeout in args", () =>
        {
            var args = FfmpegRtspDecoder.BuildArguments(new CameraRuntimeSettings { RtspHost = "127.0.0.1" });
            return args.Contains("-stimeout", StringComparison.OrdinalIgnoreCase)
                ? Task.FromResult(Fail("Contains -stimeout"))
                : Task.FromResult(Pass("Uses -timeout"));
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("FFmpeg", "RTSP timeout option", () =>
            Task.FromResult(FfmpegCapabilityProbe.SupportsRtspTimeoutOption(ffmpegPath)
                ? Pass("-timeout supported")
                : Warn("Could not verify RTSP demuxer options")), cancellationToken).ConfigureAwait(false));

        if (!string.IsNullOrWhiteSpace(options.TestMediaPath) && File.Exists(options.TestMediaPath) && options.UseTestMedia)
        {
            checks.Add(await RunCheckAsync("FFmpeg", "Process cleanup", async () =>
            {
                var before = FfmpegCapabilityProbe.CountFfmpegProcesses();
                await FfmpegCapabilityProbe.RunFilePipelineAsync(
                    ffmpegPath,
                    options.TestMediaPath!,
                    TimeSpan.FromSeconds(15),
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                var after = FfmpegCapabilityProbe.CountFfmpegProcesses();
                return after <= before ? Pass("No leak") : Fail($"{after - before} process(es) remain");
            }, cancellationToken).ConfigureAwait(false));
        }

        return checks;
    }

    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunCameraConfigChecksAsync(
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();

        if (!options.RequireCamera)
            return checks;

        var camera = await settingsService.GetCameraAsync(cancellationToken).ConfigureAwait(false);
        checks.Add(await RunCheckAsync("Camera", "Camera enabled", () =>
            Task.FromResult(camera.IsEnabled
                ? Pass("Enabled")
                : options.RequireCamera ? Fail("Disabled") : Skip("Disabled")), cancellationToken).ConfigureAwait(false));

        if (!camera.IsEnabled)
            return checks;

        checks.Add(await RunCheckAsync("Camera", "Host valid", () =>
            Task.FromResult(!string.IsNullOrWhiteSpace(camera.RtspHost) ? Pass(camera.RtspHost) : Fail("Missing")), cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "Port valid", () =>
            Task.FromResult(camera.RtspPort is > 0 and <= 65535 ? Pass(camera.RtspPort.ToString()) : Fail("Invalid")), cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "Path valid", () =>
            Task.FromResult(!string.IsNullOrWhiteSpace(camera.RtspPath) ? Pass(camera.RtspPath) : Fail("Missing")), cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "Password decrypt", async () =>
        {
            if (!camera.HasStoredPassword)
                return Skip("No stored password");
            try
            {
                var runtime = await settingsService.GetCameraRuntimeAsync(cancellationToken).ConfigureAwait(false);
                return string.IsNullOrEmpty(runtime.Password) ? Fail("Empty after decrypt") : Pass("OK (redacted)");
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }, cancellationToken).ConfigureAwait(false));

        return checks;
    }

    public async Task<IReadOnlyList<SystemDiagnosticCheck>> RunCameraConnectionChecksAsync(
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();

        if (options.SkipCamera)
            return checks;

        if (!ProductionConfigPolicy.IsHardwareMode(appSettings.DeviceMode))
        {
            checks.Add(await RunCheckAsync("Camera", "RTSP connect", () =>
                Task.FromResult(options.RequireCamera ? Fail("Not Hardware mode") : Skip("Not Hardware mode")),
                cancellationToken).ConfigureAwait(false));
            return checks;
        }

        var ffmpegPath = FfmpegPathResolver.ResolveFfmpegExecutable();
        if (ffmpegPath is null || !File.Exists(ffmpegPath))
        {
            checks.Add(await RunCheckAsync("Camera", "RTSP connect", () =>
                Task.FromResult(Fail("FFmpeg missing")), cancellationToken).ConfigureAwait(false));
            return checks;
        }

        if (options.RequireCamera)
            return await RunProductionRtspCameraChecksAsync(ffmpegPath, options, cancellationToken).ConfigureAwait(false);

        if (options.UseTestMedia
            && !string.IsNullOrWhiteSpace(options.TestMediaPath)
            && File.Exists(options.TestMediaPath))
        {
            return await RunTestMediaCameraChecksAsync(ffmpegPath, options, cancellationToken).ConfigureAwait(false);
        }

        checks.Add(await RunCheckAsync("Camera", "RTSP connect", () =>
            Task.FromResult(Skip("NOT AVAILABLE")), cancellationToken).ConfigureAwait(false));
        return checks;
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
            checks.Add(await RunCheckAsync("Scale", "COM port exists", () =>
                Task.FromResult(options.RequireScale ? Fail("Simulation mode") : Skip("Simulation mode")),
                cancellationToken).ConfigureAwait(false));
            return checks;
        }

        var scale = await settingsService.GetScaleAsync(cancellationToken).ConfigureAwait(false);
        var ports = System.IO.Ports.SerialPort.GetPortNames();
        var serialSettings = ScaleHardwareProbe.ToSerialSettings(scale);

        checks.Add(await RunCheckAsync("Scale", "COM port exists", () =>
        {
            if (ports.Length == 0)
                return Task.FromResult(options.RequireScale ? Fail("No COM ports") : Skip("NOT AVAILABLE"));
            return ports.Contains(scale.PortName, StringComparer.OrdinalIgnoreCase)
                ? Task.FromResult(Pass(scale.PortName))
                : Task.FromResult(options.RequireScale ? Fail($"{scale.PortName} not found") : Skip("Port not present"));
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Scale", "Port open", async () =>
        {
            if (!ports.Contains(scale.PortName, StringComparer.OrdinalIgnoreCase))
                return options.RequireScale ? Fail("Port not available") : Skip("Port not available");

            try
            {
                using var port = new System.IO.Ports.SerialPort(
                    scale.PortName,
                    scale.BaudRate,
                    ParseParity(scale.Parity),
                    scale.DataBits,
                    ParseStopBits(scale.StopBits));
                port.Handshake = ParseHandshake(scale.Handshake);
                port.Open();
                port.Close();
                return Pass($"Opened {scale.PortName} {scale.BaudRate} {scale.DataBits}{scale.Parity[0]}{scale.StopBits[0]}");
            }
            catch (Exception ex)
            {
                return options.RequireScale
                    ? Fail(IsComAccessDeniedMessage(ex.Message)
                        ? $"{scale.PortName} đang được tiến trình khác sử dụng"
                        : ex.Message)
                    : Skip($"NOT AVAILABLE: {ex.Message}");
            }
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Scale", "Valid frame in timeout", async () =>
        {
            if (!ports.Contains(scale.PortName, StringComparer.OrdinalIgnoreCase))
                return options.RequireScale ? Fail("Port not available") : Skip("Port not available");

            var probe = await ScaleHardwareProbe.ReadValidFrameAsync(
                serialSettings,
                DiagnosticsTimeouts.Scale,
                cancellationToken).ConfigureAwait(false);
            var detail =
                $"COM opened={probe.PortOpened}; bytes={probe.BytesReceived}; candidates={probe.CandidateFrames}; " +
                $"valid={probe.ValidFrames}; lastWeight={probe.LastValidWeightKg?.ToString() ?? "none"}; " +
                $"elapsed={probe.Elapsed.TotalSeconds:F1}s";

            if (probe.AccessDenied)
                return options.RequireScale
                    ? Fail(probe.Error ?? "COM port access denied")
                    : Skip(probe.Error ?? "COM port access denied");

            if (probe.ValidFrames > 0 && probe.LastValidWeightKg is not null)
                return Pass(detail);

            if (options.RequireScale)
                return Fail(probe.Error is null
                    ? "Không nhận được frame cân hợp lệ"
                    : $"Không nhận được frame cân hợp lệ: {probe.Error}");

            return Skip(detail);
        }, cancellationToken).ConfigureAwait(false));

        return checks;
    }

    private async Task<IReadOnlyList<SystemDiagnosticCheck>> RunProductionRtspCameraChecksAsync(
        string ffmpegPath,
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();
        var runtime = await settingsService.GetCameraRuntimeAsync(cancellationToken).ConfigureAwait(false);

        if (!runtime.IsEnabled
            || string.IsNullOrWhiteSpace(runtime.RtspHost)
            || string.IsNullOrWhiteSpace(runtime.RtspPath))
        {
            checks.Add(await RunCheckAsync("Camera", "RTSP connect", () =>
                Task.FromResult(Fail("Camera production config incomplete")), cancellationToken).ConfigureAwait(false));
            return checks;
        }

        var timeout = TimeSpan.FromSeconds(FfmpegRtspDecoder.GetConnectTimeoutSeconds(runtime));
        HardwareCameraDiagnosticsResult probe;

        try
        {
            probe = await HardwareCameraDiagnosticsProbe.RunAsync(
                runtime,
                ffmpegPath,
                timeout,
                options.CaptureCameraPipe,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            checks.Add(await RunCheckAsync("Camera", "RTSP connect", () =>
                Task.FromResult(Fail(ex.Message)), cancellationToken).ConfigureAwait(false));
            return checks;
        }

        checks.Add(await RunCheckAsync("Camera", "RTSP connect", () =>
            Task.FromResult(probe.RtspConnected
                ? Pass(probe.Endpoint)
                : Fail("RTSP connect failed")),
            cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "Video stream found", () =>
        {
            if (!probe.VideoStreamDetected)
            {
                return Task.FromResult(probe.RtspConnected
                    ? Fail("Stream detected but codec/resolution unknown")
                    : Fail("No video stream"));
            }

            return Task.FromResult(Pass($"{probe.Codec} {probe.Width}×{probe.Height}"));
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "First decoded JPEG", () =>
        {
            var frame = probe.FirstFrame;
            if (frame is null || !CameraSnapshotPolicy.LooksLikeJpeg(frame.JpegBytes))
            {
                var detail = probe.RtspConnected
                    ? "MJPEG produced but no JPEG extracted"
                    : "No JPEG from RTSP stream";
                return Task.FromResult(Fail(detail));
            }

            return Task.FromResult(Pass($"{frame.JpegBytes.Length / 1024} KB"));
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "Snapshot decode", async () =>
        {
            var frame = probe.FirstFrame;
            if (frame is null)
                return Fail("No frames");

            var jpeg = frame.JpegBytes;
            var (w, h) = SnapshotImageAnalyzer.TryReadJpegDimensions(jpeg);
            if (!CameraSnapshotPolicy.LooksLikeJpeg(jpeg) || w <= 0 || h <= 0)
                return Fail("Decode failed");

            var tempPath = Path.Combine(Path.GetTempPath(), $"canxe-diag-{Guid.NewGuid():N}.jpg");
            try
            {
                await File.WriteAllBytesAsync(tempPath, jpeg, cancellationToken).ConfigureAwait(false);
                var roundTrip = await File.ReadAllBytesAsync(tempPath, cancellationToken).ConfigureAwait(false);
                var (rw, rh) = SnapshotImageAnalyzer.TryReadJpegDimensions(roundTrip);
                if (!CameraSnapshotPolicy.LooksLikeJpeg(roundTrip) || rw != w || rh != h)
                    return Fail("Snapshot round-trip decode failed");
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }

            return Pass($"{w}×{h}");
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "FFmpeg cleanup", () =>
            Task.FromResult(probe.CleanupOk
                ? Pass("No leak")
                : Fail("FFmpeg process leak")),
            cancellationToken).ConfigureAwait(false));

        return checks;
    }

    private async Task<IReadOnlyList<SystemDiagnosticCheck>> RunTestMediaCameraChecksAsync(
        string ffmpegPath,
        DiagnosticsRunOptions options,
        CancellationToken cancellationToken)
    {
        var checks = new List<SystemDiagnosticCheck>();

        checks.Add(await RunCheckAsync("Camera", "Video stream found", async () =>
        {
            var result = await FfmpegCapabilityProbe.RunFilePipelineAsync(
                ffmpegPath, options.TestMediaPath!, TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            return result.HasFrames ? Pass($"{result.Frames.Count} frame(s)") : Fail("No frames");
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "First decoded JPEG", async () =>
        {
            var result = await FfmpegCapabilityProbe.RunFilePipelineAsync(
                ffmpegPath, options.TestMediaPath!, TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            if (!result.HasFrames)
                return Skip("No test media frames");
            var jpeg = result.Frames[0];
            return CameraSnapshotPolicy.LooksLikeJpeg(jpeg)
                ? Pass($"{jpeg.Length / 1024} KB")
                : Fail("Invalid JPEG");
        }, cancellationToken).ConfigureAwait(false));

        checks.Add(await RunCheckAsync("Camera", "Snapshot decode", async () =>
        {
            var result = await FfmpegCapabilityProbe.RunFilePipelineAsync(
                ffmpegPath, options.TestMediaPath!, TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            if (!result.HasFrames)
                return Skip("No frames");
            var jpeg = result.Frames[0];
            var (w, h) = SnapshotImageAnalyzer.TryReadJpegDimensions(jpeg);
            return CameraSnapshotPolicy.LooksLikeJpeg(jpeg) && w > 0 && h > 0
                ? Pass($"{w}x{h}")
                : Fail("Decode failed");
        }, cancellationToken).ConfigureAwait(false));

        return checks;
    }

    private static System.IO.Ports.Parity ParseParity(string value) =>
        Enum.TryParse<System.IO.Ports.Parity>(value, true, out var parsed)
            ? parsed
            : System.IO.Ports.Parity.None;

    private static System.IO.Ports.StopBits ParseStopBits(string value) =>
        Enum.TryParse<System.IO.Ports.StopBits>(value, true, out var parsed)
            ? parsed
            : System.IO.Ports.StopBits.One;

    private static System.IO.Ports.Handshake ParseHandshake(string value) =>
        Enum.TryParse<System.IO.Ports.Handshake>(value, true, out var parsed)
            ? parsed
            : System.IO.Ports.Handshake.None;

    private static bool IsComAccessDeniedMessage(string message) =>
        message.Contains("Access to the path", StringComparison.OrdinalIgnoreCase)
        || message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase);

    private static async Task<SystemDiagnosticCheck> RunCheckAsync(
        string category,
        string name,
        Func<Task<(DiagnosticStatus Status, string Detail)>> action,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (status, detail) = await action().ConfigureAwait(false);
            return new SystemDiagnosticCheck
            {
                Category = category,
                Name = name,
                Status = status,
                Detail = detail,
                Duration = sw.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return new SystemDiagnosticCheck
            {
                Category = category,
                Name = name,
                Status = DiagnosticStatus.Fail,
                Detail = $"{name} timed out",
                Duration = sw.Elapsed
            };
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
    private static (DiagnosticStatus, string) Warn(string detail) => (DiagnosticStatus.Warning, detail);
    private static (DiagnosticStatus, string) Skip(string detail) => (DiagnosticStatus.Skipped, detail);
}

internal static class SnapshotImageAnalyzer
{
    public static (int Width, int Height) TryReadJpegDimensions(byte[] jpeg)
    {
        for (var i = 0; i < jpeg.Length - 9; i++)
        {
            if (jpeg[i] != 0xFF)
                continue;
            var marker = jpeg[i + 1];
            if (marker is 0xC0 or 0xC2)
            {
                var height = (jpeg[i + 5] << 8) | jpeg[i + 6];
                var width = (jpeg[i + 7] << 8) | jpeg[i + 8];
                return (width, height);
            }
        }

        return (0, 0);
    }

    public static bool LooksLikeSolidColor(byte[] jpegBytes)
    {
        var (width, height) = TryReadJpegDimensions(jpegBytes);
        if (width <= 1 || height <= 1)
            return true;

        var pixelCount = (long)width * height;
        if (pixelCount <= 0)
            return false;

        if (jpegBytes.Length < 2048 && pixelCount > 10_000)
            return true;

        var bytesPerPixel = jpegBytes.Length / (double)pixelCount;
        return bytesPerPixel < 0.012 && jpegBytes.Length < 20_000;
    }
}
