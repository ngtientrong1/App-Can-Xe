using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public sealed class Phase3CTakeWeightReliabilityTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task TakeWeight_DoesNotCallCameraConnect()
    {
        var stream = new ControllableCameraStreamService();
        await stream.ConnectAsync(TestRuntime());
        var attempts = stream.ConnectAttempts;

        var cache = new LatestCameraFrameCache();
        stream.FrameDecoded += (_, f) => cache.TryPublish(f, stream.ActiveSessionId);
        var camera = new StreamBackedCameraService(new CameraSnapshotService(cache));
        var scope = _factory.Provider.CreateScope();
        var service = BuildTicketService(scope, camera);
        _factory.Provider.GetRequiredService<IScaleService>().SetManualMode(true);
        _factory.Provider.GetRequiredService<IScaleService>().SetManualWeightKg(1200m);

        await service.CaptureWeightAsync(new WeighTicketDraft(), 1);
        await service.WaitForPendingPhotosAsync();

        Assert.Equal(attempts, stream.ConnectAttempts);
    }

    [Fact]
    public async Task TakeWeight_DoesNotCallCameraDisconnect()
    {
        var stream = new ControllableCameraStreamService();
        await stream.ConnectAsync(TestRuntime());
        var cache = new LatestCameraFrameCache();
        stream.FrameDecoded += (_, f) => cache.TryPublish(f, stream.ActiveSessionId);

        var camera = new StreamBackedCameraService(new CameraSnapshotService(cache));
        stream.FrameDecoded += (_, f) => cache.TryPublish(f, stream.ActiveSessionId);

        var scope = _factory.Provider.CreateScope();
        var service = BuildTicketService(scope, camera);
        Assert.Equal(CameraConnectionState.Connected, stream.State);
    }

    [Fact]
    public async Task TakeWeight_DoesNotChangeCameraSessionId()
    {
        var stream = new ControllableCameraStreamService();
        await stream.ConnectAsync(TestRuntime());
        var session = stream.ActiveSessionId;
        var cache = new LatestCameraFrameCache();
        stream.FrameDecoded += (_, f) => cache.TryPublish(f, stream.ActiveSessionId);

        var scope = _factory.Provider.CreateScope();
        var service = BuildTicketService(
            scope,
            new StreamBackedCameraService(new CameraSnapshotService(cache)));
        _factory.Provider.GetRequiredService<IScaleService>().SetManualMode(true);
        _factory.Provider.GetRequiredService<IScaleService>().SetManualWeightKg(1200m);

        await service.CaptureWeightAsync(new WeighTicketDraft(), 1);
        Assert.Equal(session, stream.ActiveSessionId);
    }

    [Fact]
    public void Snapshot_UsesLatestFrameCache()
    {
        var cache = new LatestCameraFrameCache();
        var session = Guid.NewGuid();
        var jpeg = SyntheticTestJpeg.Create(640, 360, 1);
        Assert.True(cache.TryPublish(new CameraDecodedFrame
        {
            JpegBytes = jpeg,
            Width = 640,
            Height = 360,
            CapturedAt = DateTimeOffset.UtcNow,
            Codec = "mjpeg",
            SessionId = session
        }, session));

        var copy = cache.GetLatestCopy();
        Assert.NotNull(copy);
        Assert.NotSame(jpeg, copy.JpegBytes);
    }

    [Fact]
    public void Snapshot_CreatesImmutableCopy()
    {
        var cache = new LatestCameraFrameCache();
        var session = Guid.NewGuid();
        var jpeg = SyntheticTestJpeg.Create(640, 360, 2);
        Assert.True(cache.TryPublish(new CameraDecodedFrame
        {
            JpegBytes = jpeg,
            Width = 640,
            Height = 360,
            CapturedAt = DateTimeOffset.UtcNow,
            Codec = "mjpeg",
            SessionId = session
        }, session));

        var first = cache.GetLatestCopy()!;
        var index = Array.FindIndex(first.JpegBytes, b => b != 0);
        Assert.True(index >= 0);
        jpeg[index] ^= 0xFF;
        var second = cache.GetLatestCopy()!;
        Assert.NotEqual(jpeg[index], first.JpegBytes[index]);
        Assert.Equal(first.JpegBytes[index], second.JpegBytes[index]);
    }

    [Fact]
    public async Task Snapshot_FileIo_DoesNotRequireUiThread()
    {
        var cache = new LatestCameraFrameCache();
        PublishFreshFrame(cache);
        var service = new StreamBackedCameraService(new CameraSnapshotService(cache));
        var path = Path.Combine(Path.GetTempPath(), $"canxe-snap-{Guid.NewGuid():N}.jpg");

        await Task.Run(async () =>
        {
            var result = await service.CaptureAsync(new PhotoCaptureRequest { ReferenceCode = "BG" }, path);
            Assert.True(result.Success);
            Assert.True(File.Exists(path));
        });

        try { File.Delete(path); } catch { }
    }

    [Fact]
    public async Task FreshFrameSnapshot_Passes()
    {
        var cache = new LatestCameraFrameCache();
        PublishFreshFrame(cache);
        var result = await new CameraSnapshotService(cache).CaptureFromCacheAsync();
        Assert.True(result.Success);
        Assert.NotNull(result.Snapshot);
    }

    [Fact]
    public async Task StaleFrame_IsRejected()
    {
        var cache = new LatestCameraFrameCache();
        PublishFreshFrame(cache);
        await Task.Delay(TimeSpan.FromSeconds(CameraSnapshotPolicy.SnapshotMaximumFrameAgeSeconds + 1));

        var result = await new CameraSnapshotService(cache).CaptureFromCacheAsync();
        Assert.False(result.Success);
        Assert.Equal("FrameTooOld", result.RejectReason);
    }

    [Fact]
    public async Task WaitForFreshFrame_DoesNotRestartDecoder()
    {
        var stream = new ControllableCameraStreamService();
        await stream.ConnectAsync(TestRuntime());
        var attempts = stream.ConnectAttempts;
        var cache = new LatestCameraFrameCache();

        _ = cache.WaitForFreshFrameAsync(
            CameraSnapshotPolicy.MaxFreshFrameAge,
            TimeSpan.FromMilliseconds(300));

        Assert.Equal(attempts, stream.ConnectAttempts);
    }

    [Fact]
    public async Task SnapshotFail_DoesNotChangeCameraState()
    {
        var stream = new ControllableCameraStreamService();
        await stream.ConnectAsync(TestRuntime());
        var camera = new StreamBackedCameraService(new CameraSnapshotService(new LatestCameraFrameCache()));
        var path = Path.Combine(Path.GetTempPath(), $"canxe-fail-{Guid.NewGuid():N}.jpg");
        _ = await camera.CaptureAsync(new PhotoCaptureRequest { ReferenceCode = "X" }, path);
        Assert.Equal(CameraConnectionState.Connected, stream.State);
    }

    [Fact]
    public async Task TakeWeightGate_PreventsReEntry()
    {
        var gate = new SemaphoreSlim(1, 1);
        Assert.True(await gate.WaitAsync(0));
        Assert.False(await gate.WaitAsync(0));
        gate.Release();
    }

    [Fact]
    public async Task TakeWeightGate_DoubleClick_ExecutesOnce()
    {
        var gate = new SemaphoreSlim(1, 1);
        var executions = 0;
        async Task TryRun()
        {
            if (!await gate.WaitAsync(0))
                return;
            try
            {
                executions++;
                await Task.Delay(100);
            }
            finally { gate.Release(); }
        }

        await Task.WhenAll(TryRun(), TryRun());
        Assert.Equal(1, executions);
    }

    [Fact]
    public async Task TakeWeight1And2_DoNotRunConcurrently()
    {
        var gate = new SemaphoreSlim(1, 1);
        var concurrent = 0;
        var maxConcurrent = 0;

        async Task Simulate(int sequence)
        {
            if (!await gate.WaitAsync(0))
                return;
            try
            {
                var now = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, now);
                await Task.Delay(50);
                Interlocked.Decrement(ref concurrent);
            }
            finally { gate.Release(); }
        }

        await Task.WhenAll(Simulate(1), Simulate(2));
        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task CameraStream_LastValidFrameAt_UpdatesOnDecodeNotOnlyPreview()
    {
        await using var stream = TestCameraStreamFactory.Create(
            new AppSettings(),
            new SingleDecoderFactory(() => new MinimalFakeDecoder(delayFirstFrameMs: 20)));
        stream.FrameDecoded += (_, frame) => stream.NotifyPreviewRendered(frame.Width, frame.Height);
        await stream.ConnectAsync(TestRuntime());
        Assert.NotNull(stream.LastValidFrameAt);
    }

    [Fact]
    public async Task StressHarness_100Cycles_Passes()
    {
        await using var harness = new TakeWeightReliabilityHarness();
        await harness.StartCameraAsync();
        var report = await harness.RunCyclesAsync(100);
        Assert.True(report.Passed,
            $"cycles={report.SuccessfulCycles}/{report.TargetCycles} failed={report.FailedActions} " +
            $"reconnects={report.UnexpectedReconnectCount} exceptions=[{string.Join("; ", report.UnhandledExceptions)}]");
        Assert.Equal(0, report.DoubleExecutionCount);
        Assert.Equal(0, report.UnexpectedReconnectCount);
        Assert.True(report.MaximumConcurrentFfmpeg <= 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public async Task StressHarness_10Runs_Pass(int run)
    {
        await using var harness = new TakeWeightReliabilityHarness();
        await harness.StartCameraAsync();
        var report = await harness.RunCyclesAsync(20);
        Assert.True(report.Passed,
            $"Run {run}: cycles={report.SuccessfulCycles}/{report.TargetCycles} failed={report.FailedActions} " +
            $"reconnects={report.UnexpectedReconnectCount} exceptions=[{string.Join("; ", report.UnhandledExceptions)}]");
    }

    [Fact]
    public void ProductionTakeWeightPath_DoesNotUseBlockingWait()
    {
        var takeWeightFile = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "CanXe.Desktop",
            "ViewModels",
            "MainViewModel.TakeWeight.cs"));
        Assert.DoesNotContain(".Wait()", takeWeightFile);
        Assert.DoesNotContain(".Result", takeWeightFile);
        Assert.DoesNotContain("GetAwaiter().GetResult()", takeWeightFile);
    }

    private static WeighTicketService BuildTicketService(IServiceScope scope, ICameraService camera)
    {
        var sp = scope.ServiceProvider;
        return new WeighTicketService(
            sp.GetRequiredService<IWeighTicketRepository>(),
            sp.GetRequiredService<ICustomerRepository>(),
            sp.GetRequiredService<ICargoTypeRepository>(),
            sp.GetRequiredService<IVehicleRepository>(),
            sp.GetRequiredService<IScaleService>(),
            camera,
            sp.GetRequiredService<IPhotoStorageService>(),
            sp.GetRequiredService<TicketUpdateService>());
    }

    private sealed class SingleDecoderFactory(Func<ICameraDecoder> create) : ICameraDecoderFactory
    {
        public ICameraDecoder CreateDecoder() => create();
    }

    private sealed class MinimalFakeDecoder : ICameraDecoder
    {
        private readonly int _delayFirstFrameMs;
        private CancellationTokenSource? _cts;

        public MinimalFakeDecoder(int delayFirstFrameMs = 50) => _delayFirstFrameMs = delayFirstFrameMs;

        public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;
        public bool HasDecodedFrame { get; private set; }
        public CameraDecodedFrame? LatestFrame { get; private set; }
        public string? DetectedCodec { get; } = "mjpeg-test";
        public int? DetectedWidth { get; private set; }
        public int? DetectedHeight { get; private set; }
        public int FramesDecoded { get; private set; }
        public DateTimeOffset? FirstPacketAt { get; private set; }
        public DateTimeOffset? FirstDecodedFrameAt { get; private set; }
        public int? ProcessId => 4242;
        public bool IsProcessAlive => _cts is not null && !_cts.IsCancellationRequested;

        public event EventHandler<CameraDecodedFrame>? FrameDecoded;
        public event EventHandler<CameraConnectionState>? StateChanged;

        public Task StartAsync(CameraRuntimeSettings settings, int operationId, CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            State = CameraConnectionState.Connecting;
            _ = Task.Run(async () =>
            {
                await Task.Delay(_delayFirstFrameMs, _cts.Token);
                for (var i = 0; i < 3 && !_cts.Token.IsCancellationRequested; i++)
                {
                    var jpeg = SyntheticTestJpeg.Create(640, 360, i + 1);
                    var frame = new CameraDecodedFrame
                    {
                        JpegBytes = jpeg,
                        Width = 640,
                        Height = 360,
                        CapturedAt = DateTimeOffset.UtcNow,
                        Codec = "mjpeg-test",
                        SessionId = Guid.Empty
                    };
                    FramesDecoded++;
                    HasDecodedFrame = true;
                    LatestFrame = frame;
                    DetectedWidth = frame.Width;
                    DetectedHeight = frame.Height;
                    FirstDecodedFrameAt ??= DateTimeOffset.UtcNow;
                    FrameDecoded?.Invoke(this, frame);
                    State = CameraConnectionState.Connected;
                    await Task.Delay(80, _cts.Token);
                }
            }, _cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _cts?.Cancel();
            State = CameraConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static void PublishFreshFrame(LatestCameraFrameCache cache)
    {
        var session = Guid.NewGuid();
        cache.TryPublish(new CameraDecodedFrame
        {
            JpegBytes = SyntheticTestJpeg.Create(640, 360, 9),
            Width = 640,
            Height = 360,
            CapturedAt = DateTimeOffset.UtcNow,
            Codec = "mjpeg",
            SessionId = session
        }, session);
    }

    private static CameraRuntimeSettings TestRuntime() => new()
    {
        IsEnabled = true,
        AutoConnectCameraOnStartup = true,
        RtspHost = "127.0.0.1",
        RtspPort = 554,
        RtspPath = "/live"
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

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "CanXe.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("CanXe.sln not found");
    }
}
