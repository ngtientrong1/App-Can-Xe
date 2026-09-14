using System.IO.Ports;
using CanXe.Application.Models;
using CanXe.ScaleProtocol.Core;

namespace CanXe.Infrastructure.Diagnostics;

public sealed record ScaleHardwareProbeResult(
    bool PortOpened,
    long BytesReceived,
    int CandidateFrames,
    int ValidFrames,
    decimal? LastValidWeightKg,
    TimeSpan Elapsed,
    string? Error,
    bool AccessDenied);

public static class ScaleHardwareProbe
{
    public static ScaleSerialSettings ToSerialSettings(ScaleDeviceSettingsDto dto) => new()
    {
        PortName = dto.PortName,
        BaudRate = dto.BaudRate,
        DataBits = dto.DataBits,
        Parity = dto.Parity,
        StopBits = dto.StopBits,
        Handshake = dto.Handshake
    };

    public static async Task<ScaleHardwareProbeResult> ReadValidFrameAsync(
        ScaleSerialSettings settings,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var probeTask = Task.Run(() => ReadValidFrameCore(settings, timeout, cancellationToken), cancellationToken);
        // Slack beyond the probe's own internal read-loop timeout, in case Open()/Close() itself
        // hangs on a wedged USB-to-serial driver (SerialPort.Open()/Close() are not cancelable).
        var guardTimeout = timeout + TimeSpan.FromSeconds(5);
        var winner = await Task.WhenAny(probeTask, Task.Delay(guardTimeout, CancellationToken.None))
            .ConfigureAwait(false);
        if (!ReferenceEquals(winner, probeTask))
        {
            ScaleProbeDiagnosticsLogger.Write($"Probe hung beyond {guardTimeout} on {settings.PortName}");
            return new ScaleHardwareProbeResult(
                PortOpened: false,
                BytesReceived: 0,
                CandidateFrames: 0,
                ValidFrames: 0,
                LastValidWeightKg: null,
                Elapsed: guardTimeout,
                Error: $"{settings.PortName} không phản hồi — có thể driver USB-to-Serial bị treo. Hãy rút/cắm lại cáp USB rồi thử lại.",
                AccessDenied: false);
        }

        return await probeTask.ConfigureAwait(false);
    }

    private static ScaleHardwareProbeResult ReadValidFrameCore(
        ScaleSerialSettings settings,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        SerialPort? port = null;
        var portOpened = false;

        try
        {
            port = CreatePort(settings);
            ScaleProbeDiagnosticsLogger.Write($"Probe open started on {settings.PortName}");
            port.Open();
            portOpened = true;
            ScaleProbeDiagnosticsLogger.Write($"Probe open succeeded on {settings.PortName}");

            var parser = new ScaleFrameParser();
            var stability = new ScaleStabilityDetector(new ScaleStabilityOptions
            {
                ScaleDivisionKg = settings.ScaleDivisionKg
            });

            ScaleReading? latest = null;
            while (sw.Elapsed < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var available = port.BytesToRead;
                if (available > 0)
                {
                    var buffer = new byte[available];
                    var read = port.Read(buffer, 0, available);
                    if (read > 0)
                    {
                        if (read != buffer.Length)
                            Array.Resize(ref buffer, read);

                        var readings = parser.Append(buffer, DateTimeOffset.Now, stability);
                        if (readings.Count > 0)
                            latest = readings[^1];
                        if (parser.Statistics.ValidFrames > 0)
                            break;
                    }
                }

                Thread.Sleep(50);
            }

            var stats = parser.Statistics;
            return new ScaleHardwareProbeResult(
                PortOpened: portOpened,
                BytesReceived: stats.TotalBytesFed,
                CandidateFrames: stats.ValidFrames + stats.InvalidFrames,
                ValidFrames: stats.ValidFrames,
                LastValidWeightKg: latest?.WeightKg,
                Elapsed: sw.Elapsed,
                Error: null,
                AccessDenied: false);
        }
        catch (UnauthorizedAccessException ex)
        {
            return AccessDeniedResult(settings, portOpened, sw.Elapsed, ex.Message);
        }
        catch (Exception ex) when (IsComAccessDenied(ex))
        {
            return AccessDeniedResult(settings, portOpened, sw.Elapsed, ex.Message);
        }
        catch (Exception ex)
        {
            return new ScaleHardwareProbeResult(
                PortOpened: portOpened,
                BytesReceived: 0,
                CandidateFrames: 0,
                ValidFrames: 0,
                LastValidWeightKg: null,
                Elapsed: sw.Elapsed,
                Error: ex.Message,
                AccessDenied: false);
        }
        finally
        {
            if (port is not null)
            {
                try
                {
                    ScaleProbeDiagnosticsLogger.Write($"Probe close started on {settings.PortName}");
                    if (port.IsOpen)
                        port.Close();
                    port.Dispose();
                    ScaleProbeDiagnosticsLogger.Write($"Probe dispose completed on {settings.PortName}");
                }
                catch
                {
                    // Best effort.
                }
            }
        }
    }

    private static ScaleHardwareProbeResult AccessDeniedResult(
        ScaleSerialSettings settings,
        bool portOpened,
        TimeSpan elapsed,
        string message) =>
        new(
            PortOpened: portOpened,
            BytesReceived: 0,
            CandidateFrames: 0,
            ValidFrames: 0,
            LastValidWeightKg: null,
            Elapsed: elapsed,
            Error: $"{settings.PortName} đang được tiến trình khác sử dụng ({message})",
            AccessDenied: true);

    private static SerialPort CreatePort(ScaleSerialSettings settings) => new()
    {
        PortName = settings.PortName,
        BaudRate = settings.BaudRate,
        DataBits = settings.DataBits,
        Parity = ParseParity(settings.Parity),
        StopBits = ParseStopBits(settings.StopBits),
        Handshake = ParseHandshake(settings.Handshake),
        ReadTimeout = settings.ReadTimeout,
        WriteTimeout = settings.ReadTimeout,
        DtrEnable = false,
        RtsEnable = false
    };

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var parsed) ? parsed : Parity.None;

    private static StopBits ParseStopBits(string value) =>
        Enum.TryParse<StopBits>(value, true, out var parsed) ? parsed : StopBits.One;

    private static Handshake ParseHandshake(string value) =>
        Enum.TryParse<Handshake>(value, true, out var parsed) ? parsed : Handshake.None;

    private static bool IsComAccessDenied(Exception ex) =>
        ex.Message.Contains("Access to the path", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
        || (ex.InnerException is not null && IsComAccessDenied(ex.InnerException));
}

internal static class ScaleProbeDiagnosticsLogger
{
    public static void Write(string message) => DiagnosticsLogger.Write($"Scale {message}");
}
