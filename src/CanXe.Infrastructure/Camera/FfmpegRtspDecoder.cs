using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Infrastructure.Camera;

public sealed partial class FfmpegRtspDecoder : ICameraDecoder
{
    private static readonly Regex ResolutionRegex = ResolutionPattern();
    private static readonly Regex VideoCodecRegex = VideoCodecPattern();
    private static readonly Regex StreamMappingRegex = StreamMappingPattern();

    private readonly string? _ffmpegPath;
    private readonly FfmpegStderrRingBuffer _stderrBuffer = new();
    private Process? _process;
    private CancellationTokenSource? _runCts;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private TaskCompletionSource<CameraDecodedFrame?>? _firstFrameTcs;
    private int _operationId;
    private bool _disposed;
    private volatile bool _stopping;

    public FfmpegRtspDecoder(string? ffmpegPath = null) =>
        _ffmpegPath = ffmpegPath ?? FfmpegPathResolver.ResolveFfmpegExecutable();

    public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;
    public bool HasDecodedFrame { get; private set; }
    public CameraDecodedFrame? LatestFrame { get; private set; }
    public string? DetectedCodec { get; private set; }
    public int? DetectedWidth { get; private set; }
    public int? DetectedHeight { get; private set; }
    public int FramesDecoded { get; private set; }
    public DateTimeOffset? FirstPacketAt { get; private set; }
    public DateTimeOffset? FirstDecodedFrameAt { get; private set; }
    public int? ProcessId => _process is { HasExited: false } ? _process.Id : null;
    public bool IsProcessAlive => _process is { HasExited: false };
    public CameraDecoderReadDiagnostics ReadDiagnostics { get; } = new();
    public string? PipeCapturePath { get; set; }

    public event EventHandler<CameraDecodedFrame>? FrameDecoded;
    public event EventHandler<CameraConnectionState>? StateChanged;

    public Task<CameraDecodedFrame?> WaitFirstFrameAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (HasDecodedFrame && LatestFrame is not null)
            return Task.FromResult<CameraDecodedFrame?>(LatestFrame);

        var tcs = _firstFrameTcs;
        if (tcs is null)
            return Task.FromResult<CameraDecodedFrame?>(null);

        return WaitFirstFrameCoreAsync(tcs, timeout, cancellationToken);
    }

    private static async Task<CameraDecodedFrame?> WaitFirstFrameCoreAsync(
        TaskCompletionSource<CameraDecodedFrame?> tcs,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task StartAsync(
        CameraRuntimeSettings settings,
        int operationId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await StopAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_ffmpegPath) || !File.Exists(_ffmpegPath))
        {
            SetState(CameraConnectionState.Failed);
            CameraConnectionLogger.Write(
                operationId,
                "DecoderMissing",
                sanitizedEndpoint: SanitizeEndpoint(settings),
                transport: settings.RtspTransport,
                decoder: FfmpegPathResolver.DescribeResolvedPath(),
                error: "ffmpeg.exe not found in publish folder or PATH.");
            throw new InvalidOperationException("ffmpeg.exe not found.");
        }

        _operationId = operationId;
        HasDecodedFrame = false;
        LatestFrame = null;
        FramesDecoded = 0;
        FirstPacketAt = null;
        FirstDecodedFrameAt = null;
        DetectedCodec = null;
        DetectedWidth = null;
        DetectedHeight = null;
        _stopping = false;
        _firstFrameTcs = new TaskCompletionSource<CameraDecodedFrame?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SetState(CameraConnectionState.Connecting);

        var args = BuildArguments(settings);
        CameraConnectionLogger.Write(
            operationId,
            "ConnectStart",
            sanitizedEndpoint: SanitizeEndpoint(settings),
            transport: settings.RtspTransport,
            decoder: _ffmpegPath,
            snapshot: $"args={SanitizeArgsForLog(args)}");

        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _runCts.Token;

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                CreateNoWindow = true,
                StandardOutputEncoding = null,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        _process.Exited += (_, _) => OnProcessExited();

        if (!_process.Start())
        {
            SetState(CameraConnectionState.Failed);
            throw new InvalidOperationException("Failed to start ffmpeg process.");
        }

        _stderrTask = Task.Run(() => DrainStderrAsync(_process, token), token);
        _stdoutTask = Task.Run(() => ReadStdoutAsync(_process, token), token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
        {
            SetState(CameraConnectionState.Disconnected);
            return;
        }

        _stopping = true;
        try
        {
            _runCts?.Cancel();
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }

            CameraConnectionLogger.Write(
                _operationId,
                "DecoderStopped",
                exitCode: _process.ExitCode,
                framesDecoded: FramesDecoded);
        }
        catch (Exception ex)
        {
            CameraConnectionLogger.Write(_operationId, "DecoderStopError", error: ex.Message);
        }
        finally
        {
            if (_stdoutTask is not null)
                await Task.WhenAny(_stdoutTask, Task.Delay(1000, cancellationToken)).ConfigureAwait(false);
            if (_stderrTask is not null)
                await Task.WhenAny(_stderrTask, Task.Delay(1000, cancellationToken)).ConfigureAwait(false);

            _firstFrameTcs?.TrySetResult(LatestFrame);
            _firstFrameTcs = null;

            _process.Dispose();
            _process = null;
            _runCts?.Dispose();
            _runCts = null;
            SetState(CameraConnectionState.Disconnected);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }

    private void OnProcessExited()
    {
        if (_stopping)
            return;

        if (State is not (CameraConnectionState.Connected or CameraConnectionState.Connecting))
            return;

        var stderrTail = _stderrBuffer.GetTail(FfmpegDecoderDiagnostics.StderrTailLineCount);
        var error = HasDecodedFrame
            ? null
            : FfmpegDecoderDiagnostics.BuildEarlyExitError(stderrTail, _process?.ExitCode);

        CameraConnectionLogger.Write(
            _operationId,
            "DecoderExit",
            exitCode: _process?.ExitCode,
            framesDecoded: FramesDecoded,
            error: error);

        SetState(CameraConnectionState.Failed);
    }

    public string GetStderrTail(int lineCount = 20) =>
        string.Join(Environment.NewLine, _stderrBuffer.GetTail(lineCount));

    private async Task ReadStdoutAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            ReadDiagnostics.MarkStdoutReadStarted();
            await JpegStreamFrameReader.ReadFramesAsync(
                new InstrumentedStdoutStream(
                    process.StandardOutput.BaseStream,
                    ReadDiagnostics,
                    PipeCapturePath),
                OnJpegFrameAsync,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            CameraConnectionLogger.Write(_operationId, "StdoutReadError", error: ex.Message);
            if (!_stopping)
                SetState(CameraConnectionState.Failed);
        }
    }

    private Task OnJpegFrameAsync(byte[] jpeg, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FirstPacketAt ??= DateTimeOffset.UtcNow;

        var (width, height) = TryReadJpegDimensions(jpeg);
        if (width > 0 && height > 0)
        {
            DetectedWidth ??= width;
            DetectedHeight ??= height;
        }

        if (!CameraSnapshotPolicy.LooksLikeJpeg(jpeg) || !CameraSnapshotPolicy.IsValidFileSize(jpeg.Length))
            return Task.CompletedTask;

        FramesDecoded++;
        HasDecodedFrame = true;
        FirstDecodedFrameAt ??= DateTimeOffset.UtcNow;

        var frame = new CameraDecodedFrame
        {
            JpegBytes = jpeg,
            Width = width > 0 ? width : DetectedWidth ?? 0,
            Height = height > 0 ? height : DetectedHeight ?? 0,
            CapturedAt = DateTimeOffset.UtcNow,
            Codec = DetectedCodec
        };

        LatestFrame = frame;
        ReadDiagnostics.RecordExtractedFrame(jpeg, width > 0 && height > 0);
        _firstFrameTcs?.TrySetResult(frame);
        if (State != CameraConnectionState.Connected)
            SetState(CameraConnectionState.Connected);

        if (FramesDecoded == 1)
        {
            CameraConnectionLogger.Write(
                _operationId,
                "FirstDecodedFrame",
                codec: DetectedCodec,
                resolution: $"{frame.Width}×{frame.Height}",
                firstDecodedFrameAt: frame.CapturedAt,
                framesDecoded: FramesDecoded,
                snapshot: $"jpegBytes={jpeg.Length}");
        }

        FrameDecoded?.Invoke(this, frame);
        return Task.CompletedTask;
    }

    private async Task DrainStderrAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                var line = await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                    break;

                RecordStderrLine(line);
            }

            while (process.StandardError.ReadLine() is { } trailingLine)
                RecordStderrLine(trailingLine);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            CameraConnectionLogger.Write(_operationId, "StderrReadError", error: ex.Message);
        }
    }

    private void RecordStderrLine(string line)
    {
        if (ContainsCredentialLeak(line))
            return;

        _stderrBuffer.Add(line);
        ParseStderrLine(line);
    }

    private void ParseStderrLine(string line)
    {
        if (line.Contains("401", StringComparison.Ordinal)
            || line.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            CameraConnectionLogger.Write(_operationId, "AuthenticationFail", authentication: "failed", error: "RTSP unauthorized");
            return;
        }

        if (FfmpegDecoderDiagnostics.IsFatalOptionError(line))
        {
            CameraConnectionLogger.Write(_operationId, "DecoderFatal", error: RtspCredentialRedactor.Redact(line));
            return;
        }

        if (IsNonFatalDecodeWarning(line))
        {
            CameraConnectionLogger.Write(_operationId, "DecoderWarning", error: line);
            return;
        }

        var mappingMatch = StreamMappingRegex.Match(line);
        if (mappingMatch.Success)
        {
            FirstPacketAt ??= DateTimeOffset.UtcNow;
            CameraConnectionLogger.Write(
                _operationId,
                "StreamMapping",
                codec: mappingMatch.Groups[1].Value,
                snapshot: line.Trim());
            return;
        }

        if (line.Contains("Video:", StringComparison.OrdinalIgnoreCase))
        {
            FirstPacketAt ??= DateTimeOffset.UtcNow;
            var codecMatch = VideoCodecRegex.Match(line);
            if (codecMatch.Success)
                DetectedCodec = codecMatch.Groups[1].Value;

            var resMatch = ResolutionRegex.Match(line);
            if (resMatch.Success)
            {
                DetectedWidth = int.Parse(resMatch.Groups[1].Value);
                DetectedHeight = int.Parse(resMatch.Groups[2].Value);
                CameraConnectionLogger.Write(
                    _operationId,
                    "VideoStreamInfo",
                    codec: DetectedCodec,
                    resolution: $"{DetectedWidth}×{DetectedHeight}",
                    firstPacketAt: FirstPacketAt);
            }
        }
    }

    private static bool IsNonFatalDecodeWarning(string line) =>
        line.Contains("non-existing PPS", StringComparison.OrdinalIgnoreCase)
        || line.Contains("no frame!", StringComparison.OrdinalIgnoreCase)
        || line.Contains("decode_slice_header", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Waiting for keyframe", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Could not find codec parameters", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsCredentialLeak(string line) =>
        line.Contains("rtsp://", StringComparison.OrdinalIgnoreCase)
        && line.Contains('@');

    public static string BuildArguments(CameraRuntimeSettings settings)
    {
        var transport = string.Equals(settings.RtspTransport, "UDP", StringComparison.OrdinalIgnoreCase)
            ? "udp"
            : "tcp";
        var timeoutMicros = GetConnectTimeoutMicroseconds(settings);
        var url = settings.BuildRtspUrl();
        return string.Join(
            ' ',
            "-hide_banner",
            "-nostdin",
            "-loglevel info",
            $"-rtsp_transport {transport}",
            $"-timeout {timeoutMicros}",
            $"-i \"{url}\"",
            "-map 0:v:0",
            "-an",
            "-sn",
            "-dn",
            "-vf fps=12",
            "-c:v mjpeg",
            "-q:v 5",
            "-f image2pipe",
            "pipe:1");
    }

    public static int GetConnectTimeoutSeconds(CameraRuntimeSettings settings) =>
        Math.Clamp(Math.Max(settings.ConnectTimeoutSeconds, 12), 12, 120);

    public static long GetConnectTimeoutMicroseconds(CameraRuntimeSettings settings) =>
        GetConnectTimeoutSeconds(settings) * 1_000_000L;

    private static string SanitizeArgsForLog(string args)
    {
        var sanitized = RtspCredentialRedactor.Redact(args);
        return sanitized.Length > 240 ? sanitized[..240] + "…" : sanitized;
    }

    public static string SanitizeEndpoint(CameraRuntimeSettings settings)
    {
        var path = string.IsNullOrWhiteSpace(settings.RtspPath) ? "/" : settings.RtspPath;
        return $"{settings.RtspHost}:{settings.RtspPort}{path}";
    }

    private void SetState(CameraConnectionState state)
    {
        if (State == state)
            return;

        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static (int Width, int Height) TryReadJpegDimensions(byte[] jpeg)
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

    [GeneratedRegex(@"Video:\s*(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex VideoCodecPattern();

    [GeneratedRegex(@"(\d{3,5})x(\d{3,5})")]
    private static partial Regex ResolutionPattern();

    [GeneratedRegex(@"Stream mapping:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex StreamMappingPattern();
}

internal static class RtspCredentialRedactor
{
    public static string Redact(string text) =>
        Regex.Replace(
            text,
            @"rtsp://[^""'\s]+@",
            "rtsp://***@",
            RegexOptions.IgnoreCase);
}
