using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;
using CanXe.Infrastructure.Device;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase3BRealDecoderTests
{
    [Fact]
    public async Task ProcessStart_DoesNotSetStreamingUntilDecodedFrame()
    {
        await using var decoder = new RecordingFakeDecoder(delayFirstFrameMs: 200);
        var started = DateTimeOffset.UtcNow;
        _ = decoder.StartAsync(SampleSettings(), 1);

        Assert.Equal(CameraConnectionState.Connecting, decoder.State);
        Assert.False(decoder.HasDecodedFrame);

        await WaitUntilAsync(() => decoder.HasDecodedFrame, TimeSpan.FromSeconds(5));
        Assert.Equal(CameraConnectionState.Connected, decoder.State);
        Assert.True((decoder.FirstDecodedFrameAt!.Value - started).TotalMilliseconds >= 150);
    }

    [Fact]
    public async Task PacketWithoutDecode_DoesNotSetStreaming()
    {
        await using var decoder = new RecordingFakeDecoder(delayFirstFrameMs: 400, emitPacketBeforeDecode: true);
        _ = decoder.StartAsync(SampleSettings(), 1);

        Assert.Equal(CameraConnectionState.Connecting, decoder.State);
        await Task.Delay(100);
        Assert.NotNull(decoder.FirstPacketAt);
        Assert.False(decoder.HasDecodedFrame);
        Assert.Equal(CameraConnectionState.Connecting, decoder.State);

        await WaitUntilAsync(() => decoder.HasDecodedFrame, TimeSpan.FromSeconds(5));
        Assert.Equal(CameraConnectionState.Connected, decoder.State);
    }

    private static void WireAutoPreviewRender(CameraStreamService stream) =>
        stream.FrameDecoded += (_, frame) => stream.NotifyPreviewRendered(frame.Width, frame.Height);

    [Fact]
    public void FfmpegArgs_UseTimeoutNotStimeout()
    {
        var settings = new CameraRuntimeSettings
        {
            RtspHost = "192.168.1.100",
            RtspPort = 8554,
            RtspPath = "/stream",
            ConnectTimeoutSeconds = 12
        };
        var args = FfmpegRtspDecoder.BuildArguments(settings);
        Assert.Contains("-timeout 12000000", args);
        Assert.DoesNotContain("-stimeout", args, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FfmpegArgs_TimeoutMicroseconds_MatchConfiguredSeconds()
    {
        var micros = FfmpegRtspDecoder.GetConnectTimeoutMicroseconds(new CameraRuntimeSettings
        {
            ConnectTimeoutSeconds = 15
        });
        Assert.Equal(15_000_000L, micros);
        var args = FfmpegRtspDecoder.BuildArguments(new CameraRuntimeSettings
        {
            RtspHost = "10.0.0.5",
            ConnectTimeoutSeconds = 15
        });
        Assert.Contains("-timeout 15000000", args);
    }

    [Fact]
    public void EarlyExitError_LogsStderrTail_AndRedactsCredentials()
    {
        var error = FfmpegDecoderDiagnostics.BuildEarlyExitError(
        [
            "Unrecognized option 'stimeout'",
            "Error splitting the argument list: Option not found",
            "rtsp://admin:secret@192.168.1.50/live"
        ],
        exitCode: 1);

        Assert.Contains("Unrecognized option 'stimeout'", error);
        Assert.Contains("Stderr tail:", error);
        Assert.Contains("rtsp://***@", error);
        Assert.DoesNotContain("secret", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HardwareMode_UsesFfmpegDecoder_NotFake()
    {
        var factory = new CameraDecoderFactory(new AppSettings { DeviceMode = "Hardware" });
        var decoder = factory.CreateDecoder();
        Assert.IsType<FfmpegRtspDecoder>(decoder);
    }

    [Fact]
    public void FfmpegArgs_MapVideoStream_DisableAudio_NoHardcodedResolution()
    {
        var args = FfmpegRtspDecoder.BuildArguments(SampleSettings());
        Assert.Contains("-map 0:v:0", args);
        Assert.Contains("-an", args);
        Assert.DoesNotContain("1920", args);
        Assert.DoesNotContain("1080", args);
        Assert.Contains("-f image2pipe", args);
    }

    [Fact]
    public void ConnectTimeout_MinimumIsTwelveSeconds()
    {
        var timeout = FfmpegRtspDecoder.GetConnectTimeoutSeconds(new CameraRuntimeSettings
        {
            ConnectTimeoutSeconds = 5
        });
        Assert.Equal(12, timeout);
    }

    [Fact]
    public async Task FirstDecodedFrame_SetsStreamingOnStreamService()
    {
        var factory = new SingleDecoderFactory(() => new RecordingFakeDecoder(delayFirstFrameMs: 80));
        await using var stream = TestCameraStreamFactory.Create(new AppSettings(), factory);
        WireAutoPreviewRender(stream);

        Assert.Equal(CameraConnectionState.Disconnected, stream.State);
        var connected = await stream.ConnectAsync(SampleSettings());
        Assert.True(connected);
        Assert.True(stream.HasReceivedFirstFrame);
        Assert.True(stream.HasRenderedFirstFrame);
        Assert.Equal(CameraConnectionState.Connected, stream.State);
    }

    [Fact]
    public async Task FrameDecoded_RaisesEventWithRealJpeg()
    {
        await using var stream = TestCameraStreamFactory.Create(new AppSettings(), new SingleDecoderFactory(() => new RecordingFakeDecoder()));
        WireAutoPreviewRender(stream);
        CameraDecodedFrame? received = null;
        stream.FrameDecoded += (_, frame) => received = frame;

        await stream.ConnectAsync(SampleSettings());
        Assert.NotNull(received);
        Assert.True(CameraSnapshotPolicy.LooksLikeJpeg(received!.JpegBytes));
        Assert.True(CameraSnapshotPolicy.IsValidDimensions(received.Width, received.Height));
    }

    [Fact]
    public async Task Snapshot_WithoutFrame_ReturnsNull()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var stream = scope.ServiceProvider.GetRequiredService<ICameraStreamService>();
        var snapshot = await stream.CaptureSnapshotAsync();
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Snapshot_WithRealFrame_HasValidSizeAndDimensions()
    {
        await using var stream = new ControllableCameraStreamService();
        await stream.ConnectAsync(SampleSettings());
        var snapshot = await stream.CaptureSnapshotAsync();
        Assert.NotNull(snapshot);
        Assert.True(CameraSnapshotPolicy.IsValidFileSize(snapshot!.ImageBytes.Length));
        Assert.True(CameraSnapshotPolicy.IsValidDimensions(snapshot.Width, snapshot.Height));
        Assert.True(CameraSnapshotPolicy.LooksLikeJpeg(snapshot.ImageBytes));
    }

    [Fact]
    public async Task Snapshot_JpegDecodesSuccessfully()
    {
        await using var stream = new ControllableCameraStreamService();
        await stream.ConnectAsync(SampleSettings());
        var snapshot = await stream.CaptureSnapshotAsync();
        Assert.NotNull(snapshot);

        if (OperatingSystem.IsWindows())
        {
            using var ms = new MemoryStream(snapshot!.ImageBytes);
            using var image = System.Drawing.Image.FromStream(ms);
            Assert.True(image.Width >= CameraSnapshotPolicy.MinValidWidth);
            Assert.True(image.Height >= CameraSnapshotPolicy.MinValidHeight);
        }
    }

    [Fact]
    public void PlaceholderPng_70Bytes_IsRejected()
    {
        var placeholder = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        Assert.False(CameraSnapshotPolicy.IsValidFileSize(placeholder.Length));
    }

    [Fact]
    public async Task CameraLog_IsCreatedOnConnectStart()
    {
        var path = CameraConnectionLogger.LogFilePath;
        if (File.Exists(path))
            File.Delete(path);

        await using var decoder = new RecordingFakeDecoder();
        await decoder.StartAsync(SampleSettings(), 42);
        Assert.True(File.Exists(path));
        var text = await File.ReadAllTextAsync(path);
        Assert.Contains("ConnectStart", text);
        Assert.Contains("OperationId: 42", text);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizedEndpoint_DoesNotContainCredentials()
    {
        var settings = new CameraRuntimeSettings
        {
            IsEnabled = true,
            RtspHost = "192.168.1.100",
            RtspPort = 8554,
            RtspPath = "/stream",
            Username = "admin",
            Password = "secret123"
        };
        var endpoint = FfmpegRtspDecoder.SanitizeEndpoint(settings);
        Assert.Equal("192.168.1.100:8554/stream", endpoint);
        Assert.DoesNotContain("admin", endpoint);
        Assert.DoesNotContain("secret", endpoint);
    }

    [Fact]
    public async Task TestAsync_SucceedsOnlyAfterRealFrame()
    {
        var stream = TestCameraStreamFactory.Create(new AppSettings(), new SingleDecoderFactory(() => new RecordingFakeDecoder(delayFirstFrameMs: 100)));
        await using (stream)
        {
            var result = await stream.TestAsync(SampleSettings());
            Assert.True(result.Success);
            Assert.Contains("Đã nhận hình ảnh", result.Message);
            Assert.Contains("640", result.Message);
        }
    }

    [Fact]
    public async Task TestAsync_WithoutFrame_ReturnsRtspWithoutImageMessage()
    {
        var stream = TestCameraStreamFactory.Create(
            new AppSettings(),
            new SingleDecoderFactory(() => new RecordingFakeDecoder(delayFirstFrameMs: 60_000, neverDecode: true)));
        await using (stream)
        {
            var settings = new CameraRuntimeSettings
            {
                IsEnabled = true,
                RtspHost = "192.168.1.100",
                RtspPort = 8554,
                RtspPath = "/stream",
                ConnectTimeoutSeconds = 1
            };
            var result = await stream.TestAsync(settings);
            Assert.False(result.Success);
            Assert.Contains("chưa nhận được hình ảnh", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DecoderExit_TransitionsToError()
    {
        await using var decoder = new RecordingFakeDecoder(exitWithErrorAfterMs: 150);
        await decoder.StartAsync(SampleSettings(), 1);
        await WaitUntilAsync(() => decoder.State == CameraConnectionState.Failed, TimeSpan.FromSeconds(5));
        Assert.Equal(CameraConnectionState.Failed, decoder.State);
    }

    [Fact]
    public async Task DisposeAsync_CancelsDecoderDuringConnect()
    {
        var factory = new SingleDecoderFactory(() => new RecordingFakeDecoder(delayFirstFrameMs: 30_000));
        var stream = TestCameraStreamFactory.Create(new AppSettings(), factory);
        WireAutoPreviewRender(stream);
        var connectTask = stream.ConnectAsync(SampleSettings());
        await Task.Delay(150);
        await stream.DisposeAsync();
        var connected = await connectTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(connected);
        Assert.Equal(CameraConnectionState.Disconnected, stream.State);
    }

    [Fact]
    public async Task StaleFrame_TransitionsToStalledState()
    {
        var stream = new ControllableCameraStreamService();
        var supervisor = new CameraConnectionSupervisor(
            stream,
            new AppSettings { DeviceMode = "Hardware" },
            new NullScopeFactory(),
            new CameraSupervisorOptions
            {
                FrameStallWarningSeconds = 2,
                FrameStallRestartSeconds = 30,
                WatchdogIntervalSeconds = 1,
                ReconnectInitialDelaySeconds = 5
            },
            _ => Task.FromResult(SampleSettings()));

        await supervisor.StartAsync();
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Connected, TimeSpan.FromSeconds(5));
        stream.SetEmitFrames(false);
        await WaitUntilAsync(() => supervisor.State == CameraConnectionState.Stalled, TimeSpan.FromSeconds(6));
        Assert.Equal(CameraConnectionState.Stalled, supervisor.State);
        await supervisor.DisposeAsync();
    }

    private sealed class NullScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new NullScope();
    }

    private sealed class NullScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new NullProvider();
        public void Dispose() { }
    }

    private sealed class NullProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public void ScaleHeader_WaitingForData_WhenPortOpenNoFrame()
    {
        var badge = ScaleInputModeDisplay.GetHeaderScaleBadge(
            ScaleInputMode.Hardware,
            ScaleHeaderConnectionState.WaitingForData);
        Assert.Equal("● Đầu cân: Đang chờ dữ liệu", badge);
    }

    [Fact]
    public void ScaleHeader_ComConnected_IndependentOfCameraState()
    {
        var connected = ScaleInputModeDisplay.GetHeaderScaleBadge(
            ScaleInputMode.Hardware,
            ScaleHeaderConnectionState.Connected);
        var disconnected = ScaleInputModeDisplay.GetHeaderScaleBadge(
            ScaleInputMode.Hardware,
            ScaleHeaderConnectionState.Disconnected);
        Assert.Equal("● Đầu cân: COM", connected);
        Assert.Equal("● Đầu cân: Mất kết nối", disconnected);
        Assert.NotEqual(connected, disconnected);
    }

    [Fact]
    public async Task StreamBackedCamera_DoesNotWritePlaceholderWhenNoFrame()
    {
        var cache = new LatestCameraFrameCache();
        var camera = new StreamBackedCameraService(new CameraSnapshotService(cache));
        var path = Path.Combine(Path.GetTempPath(), $"canxe-no-frame-{Guid.NewGuid():N}.jpg");
        var result = await camera.CaptureAsync(new PhotoCaptureRequest { ReferenceCode = "TEST" }, path);
        Assert.False(result.Success);
        Assert.False(File.Exists(path));
    }

    private static CameraRuntimeSettings SampleSettings() => new()
    {
        IsEnabled = true,
        AutoConnectCameraOnStartup = true,
        RtspHost = "192.168.1.100",
        RtspPort = 8554,
        RtspPath = "/stream",
        Username = "admin",
        ConnectTimeoutSeconds = 5
    };

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            if (condition())
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }

    private sealed class SingleDecoderFactory(Func<ICameraDecoder> create) : ICameraDecoderFactory
    {
        public ICameraDecoder CreateDecoder() => create();
    }

    private sealed class RecordingFakeDecoder : ICameraDecoder
    {
        private readonly int _delayFirstFrameMs;
        private readonly bool _emitPacketBeforeDecode;
        private readonly bool _neverDecode;
        private readonly int? _exitWithErrorAfterMs;
        private readonly int? _maxFrames;
        private CancellationTokenSource? _cts;
        private int _framesEmitted;

        public RecordingFakeDecoder(
            int delayFirstFrameMs = 50,
            bool emitPacketBeforeDecode = false,
            bool neverDecode = false,
            int? exitWithErrorAfterMs = null,
            int? maxFrames = null)
        {
            _delayFirstFrameMs = delayFirstFrameMs;
            _emitPacketBeforeDecode = emitPacketBeforeDecode;
            _neverDecode = neverDecode;
            _exitWithErrorAfterMs = exitWithErrorAfterMs;
            _maxFrames = maxFrames;
        }

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
    public bool IsProcessAlive => _cts is not null && !_cts.IsCancellationRequested;

    public event EventHandler<CameraDecodedFrame>? FrameDecoded;
        public event EventHandler<CameraConnectionState>? StateChanged;

        public Task StartAsync(CameraRuntimeSettings settings, int operationId, CancellationToken cancellationToken = default)
        {
            CameraConnectionLogger.Write(
                operationId,
                "ConnectStart",
                sanitizedEndpoint: FfmpegRtspDecoder.SanitizeEndpoint(settings));
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            SetState(CameraConnectionState.Connecting);
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_emitPacketBeforeDecode)
                    {
                        await Task.Delay(Math.Min(_delayFirstFrameMs / 2, 80), _cts.Token);
                        FirstPacketAt = DateTimeOffset.UtcNow;
                    }

                    if (_exitWithErrorAfterMs.HasValue)
                    {
                        await Task.Delay(_exitWithErrorAfterMs.Value, _cts.Token);
                        SetState(CameraConnectionState.Failed);
                        return;
                    }

                    if (!_neverDecode)
                    {
                        await Task.Delay(_delayFirstFrameMs, _cts.Token);
                        EmitFrame();
                        while (!_maxFrames.HasValue || _framesEmitted < _maxFrames.Value)
                        {
                            await Task.Delay(80, _cts.Token);
                            EmitFrame();
                        }
                    }
                    else
                    {
                        await Task.Delay(Timeout.Infinite, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
            }, _cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _cts?.Cancel();
            SetState(CameraConnectionState.Disconnected);
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _cts?.Dispose();
        }

        private void EmitFrame()
        {
            FirstPacketAt ??= DateTimeOffset.UtcNow;
            var jpeg = SyntheticTestJpeg.Create(640, 360, FramesDecoded + 1);
            FramesDecoded++;
            _framesEmitted++;
            HasDecodedFrame = true;
            FirstDecodedFrameAt ??= DateTimeOffset.UtcNow;
            DetectedWidth = 640;
            DetectedHeight = 360;
            LatestFrame = new CameraDecodedFrame
            {
                JpegBytes = jpeg,
                Width = 640,
                Height = 360,
                CapturedAt = DateTimeOffset.UtcNow,
                Codec = DetectedCodec
            };
            SetState(CameraConnectionState.Connected);
            FrameDecoded?.Invoke(this, LatestFrame);
        }

        private void SetState(CameraConnectionState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }
}
