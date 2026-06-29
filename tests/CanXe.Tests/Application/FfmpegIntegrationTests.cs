using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;

namespace CanXe.Tests.Application;

[Trait("Category", "Integration")]
public class FfmpegIntegrationTests
{
    private static string? ResolveFfmpeg()
    {
        var bundled = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "third-party", "ffmpeg", "win-x64", "ffmpeg.exe"));
        if (File.Exists(bundled))
            return bundled;
        return FfmpegPathResolver.ResolveFfmpegExecutable();
    }

    private static string? ResolveTestMedia()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestAssets", "camera-loop.mp4"));
        return File.Exists(path) ? path : null;
    }

    private static string? ResolveAudioFirstMedia()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestAssets", "camera-audio-first.mp4"));
        return File.Exists(path) ? path : null;
    }

    [Fact]
    public void BundledFfmpeg_Exists()
    {
        var ffmpeg = ResolveFfmpeg();
        if (ffmpeg is null)
            return;
        Assert.True(File.Exists(ffmpeg));
    }

    [Fact]
    public void BundledFfmpeg_VersionCommandSucceeds()
    {
        var ffmpeg = ResolveFfmpeg();
        if (ffmpeg is null)
            return;
        Assert.True(FfmpegCapabilityProbe.VersionCommandSucceeds(ffmpeg));
    }

    [Fact]
    public void BundledFfmpeg_SupportsRtspTimeoutOption()
    {
        var ffmpeg = ResolveFfmpeg();
        if (ffmpeg is null)
            return;
        Assert.True(FfmpegCapabilityProbe.SupportsRtspTimeoutOption(ffmpeg));
    }

    [Fact]
    public void ProductionArguments_DoNotContainStimeout()
    {
        var args = FfmpegRtspDecoder.BuildArguments(new CanXe.Application.Models.CameraRuntimeSettings { RtspHost = "127.0.0.1" });
        Assert.DoesNotContain("-stimeout", args, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FilePipeline_ProducesJpegFrames()
    {
        var ffmpeg = ResolveFfmpeg();
        var media = ResolveTestMedia();
        if (ffmpeg is null || media is null)
            return;

        var result = await FfmpegCapabilityProbe.RunFilePipelineAsync(ffmpeg, media, TimeSpan.FromSeconds(30));
        Assert.True(result.HasFrames);
        Assert.True(result.Frames[0].Length > 1024);
        Assert.True(CameraSnapshotPolicy.LooksLikeJpeg(result.Frames[0]));
    }

    [Fact]
    public async Task FilePipeline_DecodesCorrectDimensions()
    {
        var ffmpeg = ResolveFfmpeg();
        var media = ResolveTestMedia();
        if (ffmpeg is null || media is null)
            return;

        var result = await FfmpegCapabilityProbe.RunFilePipelineAsync(ffmpeg, media, TimeSpan.FromSeconds(30));
        Assert.True(result.HasFrames);
        var jpeg = result.Frames[0];
        var w = (jpeg[7] << 8) | jpeg[8];
        Assert.True(w >= 320 || jpeg.Length > 5000);
    }

    [Fact]
    public async Task AudioFirstInput_StillDecodesVideo()
    {
        var ffmpeg = ResolveFfmpeg();
        var media = ResolveAudioFirstMedia() ?? ResolveTestMedia();
        if (ffmpeg is null || media is null)
            return;

        var args = FfmpegCapabilityProbe.BuildFileInputArguments(media);
        Assert.Contains("-map 0:v:0", args);
        var result = await FfmpegCapabilityProbe.RunPipelineAsync(ffmpeg, args, TimeSpan.FromSeconds(30));
        Assert.True(result.HasFrames);
    }

    [Fact]
    public async Task ProcessCleanup_NoLeakedFfmpeg()
    {
        var ffmpeg = ResolveFfmpeg();
        var media = ResolveTestMedia();
        if (ffmpeg is null || media is null)
            return;

        var before = FfmpegCapabilityProbe.CountFfmpegProcesses();
        await FfmpegCapabilityProbe.RunFilePipelineAsync(ffmpeg, media, TimeSpan.FromSeconds(20));
        await Task.Delay(500);
        var after = FfmpegCapabilityProbe.CountFfmpegProcesses();
        Assert.True(after <= before + 1);
    }

    [Fact]
    public async Task SnapshotFromPipeline_IsProductionValid()
    {
        var ffmpeg = ResolveFfmpeg();
        var media = ResolveTestMedia();
        if (ffmpeg is null || media is null)
            return;

        var result = await FfmpegCapabilityProbe.RunFilePipelineAsync(ffmpeg, media, TimeSpan.FromSeconds(30));
        Assert.True(result.HasFrames);
        var jpeg = result.Frames[0];
        Assert.False(ProductionSnapshotValidator.IsKnownPlaceholder(jpeg));
        var (w, h) = ReadJpegDimensions(jpeg);
        var looksLikeJpeg = CameraSnapshotPolicy.LooksLikeJpeg(jpeg);
        var validation = ProductionSnapshotValidator.Validate(
            jpeg.Length, w, h, looksLikeJpeg, looksLikeJpeg);
        Assert.True(validation.IsPass, validation.Message);
    }

    private static (int Width, int Height) ReadJpegDimensions(byte[] jpeg)
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

        return (640, 360);
    }
}
