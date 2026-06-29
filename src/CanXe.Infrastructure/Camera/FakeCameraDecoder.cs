using System.Text;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Infrastructure.Camera;

public sealed class FakeCameraDecoder : ICameraDecoder
{
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;
    private int _operationId;

    public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;
    public bool HasDecodedFrame { get; private set; }
    public CameraDecodedFrame? LatestFrame { get; private set; }
    public string? DetectedCodec { get; private set; } = "mjpeg-test";
    public int? DetectedWidth { get; private set; }
    public int? DetectedHeight { get; private set; }
    public int FramesDecoded { get; private set; }
    public DateTimeOffset? FirstPacketAt { get; private set; }
    public DateTimeOffset? FirstDecodedFrameAt { get; private set; }
    public int? ProcessId => null;
    public bool IsProcessAlive => _loop is { IsCompleted: false };

    public event EventHandler<CameraDecodedFrame>? FrameDecoded;
    public event EventHandler<CameraConnectionState>? StateChanged;

    public Task StartAsync(
        CameraRuntimeSettings settings,
        int operationId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _operationId = operationId;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        SetState(CameraConnectionState.Connecting);

        CameraConnectionLogger.Write(
            operationId,
            "ConnectStart",
            sanitizedEndpoint: FfmpegRtspDecoder.SanitizeEndpoint(settings),
            transport: settings.RtspTransport,
            decoder: "FakeCameraDecoder");

        _loop = Task.Run(() => RunAsync(settings, _cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null)
        {
            SetState(CameraConnectionState.Disconnected);
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
            await Task.WhenAny(_loop, Task.Delay(500, cancellationToken)).ConfigureAwait(false);

        _cts.Dispose();
        _cts = null;
        SetState(CameraConnectionState.Disconnected);
        CameraConnectionLogger.Write(_operationId, "DecoderStopped");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunAsync(CameraRuntimeSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(80, cancellationToken).ConfigureAwait(false);
            FirstPacketAt = DateTimeOffset.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                var jpeg = SyntheticJpegEncoder.CreateFrame(640, 360, FramesDecoded);
                if (!CameraSnapshotPolicy.IsValidFileSize(jpeg.Length))
                    continue;

                FramesDecoded++;
                HasDecodedFrame = true;
                FirstDecodedFrameAt ??= DateTimeOffset.UtcNow;
                DetectedWidth = 640;
                DetectedHeight = 360;

                var frame = new CameraDecodedFrame
                {
                    JpegBytes = jpeg,
                    Width = 640,
                    Height = 360,
                    CapturedAt = DateTimeOffset.UtcNow,
                    Codec = DetectedCodec
                };

                LatestFrame = frame;
                if (State != CameraConnectionState.Connected)
                    SetState(CameraConnectionState.Connected);

                FrameDecoded?.Invoke(this, frame);
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }
    }

    private void SetState(CameraConnectionState state)
    {
        if (State == state)
            return;

        State = state;
        StateChanged?.Invoke(this, state);
    }
}

internal static class SyntheticJpegEncoder
{
    public static byte[] CreateFrame(int width, int height, int frameIndex)
    {
        if (!OperatingSystem.IsWindows())
            return MinimalJpegBuilder.Build(width, height, frameIndex);

        using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        var color = System.Drawing.Color.FromArgb(
            (frameIndex * 37) % 255,
            (frameIndex * 53) % 255,
            (frameIndex * 71) % 255);
        graphics.Clear(color);
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        return ms.ToArray();
    }
}

internal static class MinimalJpegBuilder
{
    public static byte[] Build(int width, int height, int frameIndex)
    {
        var payload = new byte[640 + frameIndex * 16];
        Array.Fill(payload, (byte)(frameIndex % 255));
        var header = Encoding.ASCII.GetBytes($"CANXE-FAKE-{width}x{height}-");
        Array.Copy(header, payload, Math.Min(header.Length, payload.Length));
        return payload;
    }
}
