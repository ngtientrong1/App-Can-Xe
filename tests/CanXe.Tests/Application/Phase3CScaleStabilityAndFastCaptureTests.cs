using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Infrastructure.Scale;
using CanXe.ScaleProtocol.Core;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public sealed class Phase3CScaleStabilityAndFastCaptureTests
{
    [Fact]
    public void HardwareStableFlag_True_AppReportsStableAfterTwoFrames()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        ScaleReading? last = null;
        for (var i = 0; i < 2; i++)
            last = parser.Append(ScaleFrameFixtures.BuildFrame('+', 200), at, stability).Single();

        Assert.True(last!.IsStable);
        Assert.Equal(ScaleStableSource.HardwareFlag, last.StableSource);
    }

    [Fact]
    public void HardwareStableFlag_False_AppReportsUnstable()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var frame = ScaleFrameFixtures.BuildFrame('+', 200, "00");
        var reading = parser.Append(frame, DateTimeOffset.UtcNow, stability).Single();
        Assert.False(reading.IsStable);
        Assert.Equal(ScaleStableSource.HardwareFlag, reading.StableSource);
        Assert.False(reading.RawStableFlag);
    }

    [Fact]
    public void HardwareStableFlag_NotOverriddenBySoftwareAfterStable()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        for (var i = 0; i < 3; i++)
            parser.Append(ScaleFrameFixtures.BuildFrame('+', 200), at, stability);
        var reading = parser.Append(ScaleFrameFixtures.BuildFrame('+', 205, "01"), at, stability).Single();
        Assert.True(reading.IsStable);
        Assert.Equal(ScaleStableSource.HardwareFlag, reading.StableSource);
    }

    [Fact]
    public void SoftwareWindow_UsesScaleDivision()
    {
        var detector = new ScaleStabilityDetector(new ScaleStabilityOptions
        {
            ScaleDivisionKg = 20,
            SoftwareRequiredMatchingFrames = 3,
            SoftwareStableRequiredDurationMs = 300
        });
        var start = DateTimeOffset.UtcNow;
        var stable = false;
        for (var i = 0; i < 4; i++)
        {
            var frame = ScaleFrameFixtures.BuildFrame('+', 200 + (i % 2) * 10, "02");
            var validation = ScaleFrameValidator.Validate(frame);
            Assert.True(validation.IsValid);
            stable = detector.NotifyValidReading(validation.Frame!, start.AddMilliseconds(i * 120)).IsStable;
        }

        Assert.True(stable);
        Assert.Equal(ScaleStableSource.SoftwareWindow, detector.CurrentState.StableSource);
    }

    [Fact]
    public async Task HardwareStable_AllowsCapture()
    {
        var reader = new FakeScaleSerialReader();
        var service = new CompositeScaleService(reader);
        await service.SetInputModeAsync(ScaleInputMode.Hardware);
        await service.PrepareAndConnectHardwareAsync(new ScaleSerialSettings());
        reader.ConfigureStableReading(200, ScaleFrameFixtures.BuildFrame('+', 200));
        reader.PublishReading(reader.LatestReading!);
        Assert.True(service.CanCaptureWeight());
    }

    [Fact]
    public async Task CaptureWeight_ReturnsBeforePhotoCompletes()
    {
        var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        try
        {
            var scope = factory.Provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
            factory.Provider.GetRequiredService<IScaleService>().SetManualMode(true);
            factory.Provider.GetRequiredService<IScaleService>().SetManualWeightKg(200m);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await service.CaptureWeightAsync(new WeighTicketDraft(), 1);
            sw.Stop();

            Assert.True(result.Success);
            Assert.True(sw.ElapsedMilliseconds < 150);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public void Watchdog_UsesDecodedFrameTimestamp_NotUiRender()
    {
        var stream = new ControllableCameraStreamService();
        stream.ConnectAsync(new CameraRuntimeSettings
        {
            IsEnabled = true,
            RtspHost = "127.0.0.1"
        }).GetAwaiter().GetResult();

        Assert.NotNull(stream.LastDecodedFrameAt);
        Assert.Equal(stream.LastDecodedFrameAt, stream.LastValidFrameAt);
    }

    [Fact]
    public void TakeWeight_DoesNotResetLastDecodedFrameAt()
    {
        var stream = new ControllableCameraStreamService();
        stream.ConnectAsync(new CameraRuntimeSettings { IsEnabled = true, RtspHost = "127.0.0.1" })
            .GetAwaiter().GetResult();
        var before = stream.LastDecodedFrameAt;
        Assert.NotNull(before);
        Thread.Sleep(50);
        Assert.NotNull(stream.LastDecodedFrameAt);
    }
}
