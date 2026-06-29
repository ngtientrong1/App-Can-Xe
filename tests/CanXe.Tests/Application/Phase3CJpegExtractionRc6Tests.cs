using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;

namespace CanXe.Tests.Application;

public class Phase3CJpegExtractionRc6Tests
{
    [Fact]
    public void EoiInDqtSegment_NaiveSearchWouldFalsePositive_MarkerAwareExtractsRealFrame()
    {
        var realJpeg = CreateMinimalJpeg(64, 64);
        var fakeDqt = new byte[]
        {
            0xFF, 0xDB, 0x00, 0x06, 0xFF, 0xD9, 0x00, 0x00
        };
        var combined = fakeDqt.Concat(realJpeg).ToArray();

        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(combined, out var frame, out var consumed));
        Assert.Equal(realJpeg.Length, frame.Length);
        Assert.Equal(fakeDqt.Length + realJpeg.Length, consumed);
    }

    [Fact]
    public void EoiFound_ProducesExtractedFrame_WithSoiAndEoiTelemetry()
    {
        var jpeg = CreateMinimalJpeg(128, 96);
        var frames = JpegStreamFrameReader.ExtractAllFrames(jpeg);
        Assert.Single(frames);

        var diagnostics = new CameraDecoderReadDiagnostics();
        diagnostics.RecordExtractedFrame(frames[0], decodeSuccess: true);
        Assert.True(diagnostics.JpegSoiFound);
        Assert.True(diagnostics.JpegEoiFound);
        Assert.True(diagnostics.JpegExtracted);
    }

    [Fact]
    public void FrameBuffer_NotClearedBeforeCopy()
    {
        var jpeg = CreateMinimalJpeg(48, 36);
        var frames = JpegStreamFrameReader.ExtractAllFrames(jpeg);
        Assert.Equal(jpeg, frames[0]);
    }

    [Fact]
    public async Task TwoFramesInOneStream_BothExtracted()
    {
        var a = CreateMinimalJpeg(32, 32);
        var b = CreateMinimalJpeg(40, 40);
        await using var stream = new MemoryStream(a.Concat(b).ToArray());
        var frames = new List<byte[]>();
        await JpegStreamFrameReader.ReadFramesAsync(
            stream,
            (frame, _) =>
            {
                frames.Add(frame);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(2, frames.Count);
        Assert.Equal(a.Length, frames[0].Length);
        Assert.Equal(b.Length, frames[1].Length);
    }

    [Fact]
    public void Frame2304x1296_ExtractsWithCorrectDimensions()
    {
        var jpeg = CreateMinimalJpeg(2304, 1296);
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(jpeg, out var frame, out _));
        var (w, h) = ReadDimensions(frame);
        Assert.Equal(2304, w);
        Assert.Equal(1296, h);
        Assert.True(CameraSnapshotPolicy.LooksLikeJpeg(frame.ToArray()));
    }

    [Fact]
    public async Task RealFfmpegMjpeg2304x1296_FirstFrameWithin3Seconds()
    {
        var ffmpeg = FfmpegPathResolver.ResolveFfmpegExecutable();
        if (ffmpeg is null || !File.Exists(ffmpeg))
            return;

        var args =
            "-hide_banner -nostdin -loglevel error -f lavfi " +
            "-i mandelbrot=size=2304x1296:rate=12 " +
            "-vf \"fps=12,noise=alls=20:allf=t+u\" -c:v mjpeg -q:v 1 -f image2pipe pipe:1";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var pipeline = await FfmpegCapabilityProbe.RunPipelineAsync(ffmpeg, args, TimeSpan.FromSeconds(3), cts.Token);
        Assert.True(pipeline.HasFrames, pipeline.StderrTail);
        var first = pipeline.Frames[0];
        Assert.True(first.Length > 500 * 1024, $"Frame too small: {first.Length}");
        Assert.Equal(0xFF, first[0]);
        Assert.Equal(0xD8, first[1]);
        Assert.Equal(0xFF, first[^2]);
        Assert.Equal(0xD9, first[^1]);

        var (w, h) = ReadDimensions(first);
        Assert.Equal(2304, w);
        Assert.Equal(1296, h);

        if (OperatingSystem.IsWindows())
        {
            using var ms = new MemoryStream(first);
            using var image = System.Drawing.Image.FromStream(ms);
            Assert.Equal(2304, image.Width);
            Assert.Equal(1296, image.Height);
        }

        if (pipeline.Frames.Count > 1)
            Assert.True(pipeline.Frames[1].Length > 1000);
    }

    [Fact]
    public void ParseCaptureFile_ExtractsFrames()
    {
        var jpeg = CreateMinimalJpeg(640, 480);
        var path = Path.Combine(Path.GetTempPath(), $"canxe-parse-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, jpeg);
        try
        {
            var result = CameraPipeCapture.ParseFile(path);
            Assert.True(result.Success);
            Assert.Equal(1, result.FrameCount);
            Assert.Equal(640, result.Width);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RtspConnectResult_IndependentFromJpegExtract()
    {
        var diagnostics = new CameraDecoderReadDiagnostics();
        diagnostics.RecordChunk([0xFF, 0xD8, 0xFF, 0xFE, 0x00, 0x10]);
        Assert.False(diagnostics.JpegExtracted);
    }

    private static (int Width, int Height) ReadDimensions(ReadOnlySpan<byte> jpeg)
    {
        for (var i = 0; i < jpeg.Length - 9; i++)
        {
            if (jpeg[i] != 0xFF)
                continue;
            if (jpeg[i + 1] is 0xC0 or 0xC2)
                return ((jpeg[i + 7] << 8) | jpeg[i + 8], (jpeg[i + 5] << 8) | jpeg[i + 6]);
        }

        return (0, 0);
    }

    private static byte[] CreateMinimalJpeg(int width, int height)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD8);
        ms.WriteByte(0xFF);
        ms.WriteByte(0xC0);
        ms.WriteByte(0x00);
        ms.WriteByte(0x11);
        ms.WriteByte(0x08);
        ms.WriteByte((byte)(height >> 8));
        ms.WriteByte((byte)(height & 0xFF));
        ms.WriteByte((byte)(width >> 8));
        ms.WriteByte((byte)(width & 0xFF));
        ms.WriteByte(0x03);
        ms.WriteByte(0x01);
        ms.WriteByte(0x11);
        ms.WriteByte(0x00);
        ms.WriteByte(0x02);
        ms.WriteByte(0x11);
        ms.WriteByte(0x01);
        ms.WriteByte(0x03);
        ms.WriteByte(0x11);
        ms.WriteByte(0x01);
        ms.WriteByte(0xFF);
        ms.WriteByte(0xDA);
        ms.WriteByte(0x00);
        ms.WriteByte(0x08);
        ms.WriteByte(0x01);
        ms.WriteByte(0x01);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x3F);
        ms.WriteByte(0x00);
        ms.WriteByte(0xAB);
        ms.WriteByte(0xCD);
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD9);
        return ms.ToArray();
    }
}
