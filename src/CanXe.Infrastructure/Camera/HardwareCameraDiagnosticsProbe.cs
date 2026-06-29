using CanXe.Application.Models;
using CanXe.Infrastructure.Diagnostics;

namespace CanXe.Infrastructure.Camera;

public sealed record HardwareCameraDiagnosticsResult(
    bool RtspConnected,
    bool VideoStreamDetected,
    string Endpoint,
    string? Codec,
    int? Width,
    int? Height,
    CameraDecodedFrame? FirstFrame,
    CameraDecoderReadDiagnostics ReadDiagnostics,
    string StderrTailForReport,
    bool CleanupOk,
    string? Error,
    string? CapturePath = null);

public static class HardwareCameraDiagnosticsProbe
{
    public static async Task<HardwareCameraDiagnosticsResult> RunAsync(
        CameraRuntimeSettings settings,
        string? ffmpegPath,
        TimeSpan firstFrameTimeout,
        bool capturePipe = false,
        CancellationToken cancellationToken = default)
    {
        var endpoint = FfmpegRtspDecoder.SanitizeEndpoint(settings);
        var beforeProcesses = FfmpegCapabilityProbe.CountFfmpegProcesses();
        await using var decoder = new FfmpegRtspDecoder(ffmpegPath);

        string? capturePath = capturePipe ? CameraPipeCapture.DefaultCapturePath : null;
        decoder.PipeCapturePath = capturePath;

        CameraDecodedFrame? firstFrame = null;
        void OnFrame(object? _, CameraDecodedFrame frame) => firstFrame ??= frame;
        decoder.FrameDecoded += OnFrame;

        try
        {
            await decoder.StartAsync(settings, operationId: 9001, cancellationToken).ConfigureAwait(false);
            firstFrame = await decoder.WaitFirstFrameAsync(firstFrameTimeout, cancellationToken).ConfigureAwait(false)
                ?? firstFrame;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BuildResult(
                decoder, endpoint, beforeProcesses, firstFrame, capturePath, ex.Message);
        }
        finally
        {
            decoder.FrameDecoded -= OnFrame;
            try
            {
                using var cleanupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cleanupCts.CancelAfter(DiagnosticsTimeouts.Cleanup);
                await decoder.StopAsync(cleanupCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                DiagnosticsLogger.Write("Camera decoder cleanup timed out");
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        try
        {
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore post-cleanup delay timeout.
        }
        return BuildResult(decoder, endpoint, beforeProcesses, firstFrame, capturePath, null);
    }

    private static HardwareCameraDiagnosticsResult BuildResult(
        FfmpegRtspDecoder decoder,
        string endpoint,
        int beforeProcesses,
        CameraDecodedFrame? firstFrame,
        string? capturePath,
        string? error)
    {
        var afterProcesses = FfmpegCapabilityProbe.CountFfmpegProcesses();
        var stderrTail = decoder.GetStderrTail(30);
        var readDiagnostics = decoder.ReadDiagnostics;

        DiagnosticsLogger.Write($"Camera {readDiagnostics.Summarize()}");
        if (!string.IsNullOrWhiteSpace(stderrTail))
        {
            DiagnosticsLogger.Write(
                $"Camera stderr tail:{Environment.NewLine}{RtspCredentialSanitizer.Redact(stderrTail)}");
        }

        if (!string.IsNullOrWhiteSpace(capturePath) && File.Exists(capturePath))
            DiagnosticsLogger.Write($"Camera pipe capture: {capturePath}");

        var rtspConnected = decoder.FirstPacketAt is not null
            || decoder.DetectedCodec is not null
            || decoder.DetectedWidth is not null
            || readDiagnostics.StdoutBytesRead > 0;

        var videoDetected = decoder.DetectedCodec is not null
            && decoder.DetectedWidth is > 0
            && decoder.DetectedHeight is > 0;

        return new HardwareCameraDiagnosticsResult(
            RtspConnected: rtspConnected,
            VideoStreamDetected: videoDetected,
            Endpoint: endpoint,
            Codec: decoder.DetectedCodec,
            Width: decoder.DetectedWidth,
            Height: decoder.DetectedHeight,
            FirstFrame: firstFrame ?? decoder.LatestFrame,
            ReadDiagnostics: readDiagnostics,
            StderrTailForReport: stderrTail,
            CleanupOk: afterProcesses <= beforeProcesses,
            Error: error,
            CapturePath: capturePath);
    }
}
