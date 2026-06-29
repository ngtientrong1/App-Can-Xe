using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;

namespace CanXe.Tests.Application;

[Trait("Category", "Integration")]
public class TestAssetsMetadataTests
{
    private static string? ResolveFfmpegDir()
    {
        var bundled = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "third-party", "ffmpeg", "win-x64"));
        return Directory.Exists(bundled) ? bundled : null;
    }

    private static string ResolveAsset(string fileName)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestAssets", fileName)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tests", "TestAssets", fileName))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return candidates[0];
    }

    [Fact]
    public void CameraLoop_Exists_And_Is1280x720()
    {
        var path = ResolveAsset("camera-loop.mp4");
        Assert.True(File.Exists(path), path);

        var info = FfmpegMediaProbe.Probe(path, ResolveFfprobe());
        Assert.NotNull(info);
        Assert.Equal(1280, info!.VideoWidth);
        Assert.Equal(720, info.VideoHeight);
        Assert.InRange(info.DurationSeconds, 3.0, 5.5);
        Assert.Contains("video", info.StreamCodecTypes);
    }

    [Fact]
    public void CameraAudioFirst_Exists_Stream0IsAudio_Stream1IsVideo_1280x720()
    {
        var path = ResolveAsset("camera-audio-first.mp4");
        Assert.True(File.Exists(path), path);

        var info = FfmpegMediaProbe.Probe(path, ResolveFfprobe());
        Assert.NotNull(info);
        Assert.Equal(1280, info!.VideoWidth);
        Assert.Equal(720, info.VideoHeight);
        Assert.InRange(info.DurationSeconds, 3.0, 5.5);
        Assert.True(info.StreamCodecTypes.Count >= 2, string.Join(", ", info.StreamCodecTypes));
        Assert.Equal("audio", info.StreamCodecTypes[0]);
        Assert.Equal("video", info.StreamCodecTypes[1]);
    }

    [Fact]
    public async Task AudioFirstInput_MapVideoStream_DecodesWithFfmpeg()
    {
        var ffmpeg = FfmpegPathResolver.ResolveFfmpegExecutable()
            ?? Path.Combine(ResolveFfmpegDir() ?? "", "ffmpeg.exe");
        if (!File.Exists(ffmpeg))
            return;

        var path = ResolveAsset("camera-audio-first.mp4");
        if (!File.Exists(path))
            return;

        var args = FfmpegCapabilityProbe.BuildFileInputArguments(path);
        Assert.Contains("-map 0:v:0", args);
        var result = await FfmpegCapabilityProbe.RunPipelineAsync(ffmpeg, args, TimeSpan.FromSeconds(30));
        Assert.True(result.HasFrames);
    }

    private static string? ResolveFfprobe() =>
        FfmpegMediaProbe.ResolveFfprobe();
}

public class Phase3CSnapshotValidationPolicyTests
{
    [Fact]
    public void ValidSmallJpeg_ReturnsWarning_NotFail()
    {
        var result = ProductionSnapshotValidator.Validate(
            fileSize: 4096,
            width: 1280,
            height: 720,
            decodable: true,
            looksLikeJpeg: true);
        Assert.Equal(SnapshotValidationSeverity.Warning, result.Severity);
    }

    [Fact]
    public void OneByOne_ReturnsFail()
    {
        var result = ProductionSnapshotValidator.Validate(5000, 1, 1, true, true);
        Assert.Equal(SnapshotValidationSeverity.Fail, result.Severity);
    }

    [Fact]
    public void TooSmallDimensions_ReturnsFail()
    {
        var result = ProductionSnapshotValidator.Validate(50_000, 320, 180, true, true);
        Assert.Equal(SnapshotValidationSeverity.Fail, result.Severity);
    }

    [Fact]
    public void DecodeFail_ReturnsFail()
    {
        var result = ProductionSnapshotValidator.Validate(5000, 640, 360, false, false);
        Assert.Equal(SnapshotValidationSeverity.Fail, result.Severity);
    }

    [Fact]
    public void LargeValidJpeg_ReturnsPass()
    {
        var result = ProductionSnapshotValidator.Validate(20_000, 1280, 720, true, true);
        Assert.Equal(SnapshotValidationSeverity.Pass, result.Severity);
    }
}
