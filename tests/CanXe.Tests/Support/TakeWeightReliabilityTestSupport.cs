using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure;
using CanXe.Infrastructure.Camera;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Support;

public sealed class TakeWeightReliabilityHarness : IAsyncDisposable
{
    private readonly ControllableCameraStreamService _stream;
    private readonly LatestCameraFrameCache _cache;
    private readonly StreamBackedCameraService _cameraService;
    private readonly WeighTicketService _ticketService;
    private readonly IScaleService _scale;
    private readonly ServiceProvider _provider;
    private readonly SemaphoreSlim _takeWeightGate = new(1, 1);
    private int _doubleExecutionCount;
    private int _connectAttemptsAtStart = -1;

    public TakeWeightReliabilityHarness()
    {
        _stream = new ControllableCameraStreamService();
        _cache = new LatestCameraFrameCache();
        var snapshotService = new CameraSnapshotService(_cache);
        _cameraService = new StreamBackedCameraService(snapshotService);

        var dbPath = Path.Combine(Path.GetTempPath(), $"canxe-tw-stress-{Guid.NewGuid():N}.db");
        var photoRoot = Path.Combine(Path.GetTempPath(), $"canxe-tw-photos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(photoRoot);

        var services = new ServiceCollection();
        services.AddCanXeInfrastructure(new AppSettings(), dbPath, photoRoot);
        var existingCamera = services.First(d => d.ServiceType == typeof(ICameraService));
        services.Remove(existingCamera);
        services.AddSingleton<ICameraService>(_cameraService);

        _provider = services.BuildServiceProvider();
        DependencyInjection.InitializeDatabaseAsync(_provider, dbPath).GetAwaiter().GetResult();

        _ticketService = _provider.CreateScope().ServiceProvider.GetRequiredService<WeighTicketService>();
        _scale = _provider.GetRequiredService<IScaleService>();
        _scale.SetManualMode(true);
    }

    public ControllableCameraStreamService Stream => _stream;
    public LatestCameraFrameCache Cache => _cache;
    public int DoubleExecutionCount => _doubleExecutionCount;

    public async Task StartCameraAsync()
    {
        _stream.FrameDecoded += OnFrameDecoded;
        await _stream.ConnectAsync(new CameraRuntimeSettings
        {
            IsEnabled = true,
            AutoConnectCameraOnStartup = true,
            RtspHost = "127.0.0.1",
            RtspPort = 554,
            RtspPath = "/live"
        }).ConfigureAwait(false);

        if (_stream.LatestFrame is { } latest && !_cache.TryPublish(latest, _stream.ActiveSessionId))
            throw new InvalidOperationException("Failed to seed latest camera frame into cache.");

        _connectAttemptsAtStart = _stream.ConnectAttempts;
    }

    public async Task<TakeWeightStressReport> RunCyclesAsync(int cycles, Random? random = null)
    {
        random ??= new Random(42);
        var report = new TakeWeightStressReport { TargetCycles = cycles };
        var sessionBefore = _stream.ActiveSessionId;
        var maxCommand = 0L;

        for (var i = 0; i < cycles; i++)
        {
            report.TotalButtonActions++;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await CaptureWithGateAsync(1).ConfigureAwait(false);
                maxCommand = Math.Max(maxCommand, sw.ElapsedMilliseconds);
                await Task.Delay(random.Next(50, 150)).ConfigureAwait(false);

                report.TotalButtonActions++;
                sw.Restart();
                if (i % 2 == 0)
                    await CaptureWithGateAsync(1).ConfigureAwait(false);
                maxCommand = Math.Max(maxCommand, sw.ElapsedMilliseconds);
                await Task.Delay(random.Next(50, 150)).ConfigureAwait(false);

                report.TotalButtonActions++;
                sw.Restart();
                await CaptureWithGateAsync(2).ConfigureAwait(false);
                maxCommand = Math.Max(maxCommand, sw.ElapsedMilliseconds);

                report.SuccessfulCycles++;
                await Task.Delay(random.Next(50, 200)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                report.FailedActions++;
                report.UnhandledExceptions.Add(ex.Message);
            }
        }

        report.CameraFrameCount = _stream.FramesDecoded;
        report.LongestFrameGap = _stream.LastFrameAge ?? TimeSpan.Zero;
        report.UnexpectedReconnectCount = Math.Max(0, _stream.ConnectAttempts - (_connectAttemptsAtStart < 0 ? 0 : _connectAttemptsAtStart));
        report.FfmpegRestartCount = report.UnexpectedReconnectCount;
        report.MaximumConcurrentFfmpeg = _stream.MaxConcurrentConnectOperations;
        report.DoubleExecutionCount = _doubleExecutionCount;
        report.UiCommandMaxDurationMs = maxCommand;
        report.SessionIdChanged = _stream.ActiveSessionId != sessionBefore;
        return report;
    }

    private void OnFrameDecoded(object? sender, CameraDecodedFrame frame) =>
        _cache.TryPublish(frame, _stream.ActiveSessionId);

    private async Task CaptureWithGateAsync(int sequence)
    {
        if (!await _takeWeightGate.WaitAsync(0).ConfigureAwait(false))
        {
            Interlocked.Increment(ref _doubleExecutionCount);
            return;
        }

        try
        {
            _scale.SetManualWeightKg(8000m + sequence * 1000m);
            var draft = new WeighTicketDraft();
            var result = await _ticketService.CaptureWeightAsync(draft, sequence).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(result.ErrorMessage ?? "Capture failed");

            await _ticketService.WaitForPendingPhotosAsync().ConfigureAwait(false);
            var status = sequence == 1 ? draft.DraftWeight1PhotoStatus : draft.DraftWeight2PhotoStatus;
            if (status != DraftPhotoStatus.Valid)
            {
                var error = sequence == 1 ? draft.DraftWeight1PhotoError : draft.DraftWeight2PhotoError;
                throw new InvalidOperationException($"Snapshot not valid for sequence {sequence}: {error ?? "unknown"}");
            }
        }
        finally
        {
            _takeWeightGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stream.FrameDecoded -= OnFrameDecoded;
        await _stream.DisposeAsync().ConfigureAwait(false);
        _takeWeightGate.Dispose();
        await _provider.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed class TakeWeightStressReport
{
    public int TargetCycles { get; set; }
    public int TotalButtonActions { get; set; }
    public int SuccessfulCycles { get; set; }
    public int FailedActions { get; set; }
    public int CameraFrameCount { get; set; }
    public TimeSpan LongestFrameGap { get; set; }
    public int UnexpectedReconnectCount { get; set; }
    public int FfmpegRestartCount { get; set; }
    public int MaximumConcurrentFfmpeg { get; set; }
    public long UiCommandMaxDurationMs { get; set; }
    public int DoubleExecutionCount { get; set; }
    public bool SessionIdChanged { get; set; }
    public List<string> UnhandledExceptions { get; } = [];

    public bool Passed =>
        UnhandledExceptions.Count == 0
        && DoubleExecutionCount == 0
        && FfmpegRestartCount == 0
        && UnexpectedReconnectCount == 0
        && !SessionIdChanged
        && MaximumConcurrentFfmpeg <= 1
        && FailedActions == 0
        && SuccessfulCycles == TargetCycles;
}
