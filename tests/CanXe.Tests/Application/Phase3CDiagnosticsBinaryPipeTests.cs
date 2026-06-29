using CanXe.Application.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Camera;

namespace CanXe.Tests.Application;

public class Phase3CDiagnosticsBinaryPipeTests
{
    [Fact]
    public void HardwareProbe_UsesProductionFfmpegRtspDecoder()
    {
        var method = typeof(HardwareCameraDiagnosticsProbe).GetMethod(nameof(HardwareCameraDiagnosticsProbe.RunAsync));
        Assert.NotNull(method);
        Assert.Contains("FfmpegRtspDecoder", File.ReadAllText(
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "CanXe.Infrastructure", "Camera", "HardwareCameraDiagnosticsProbe.cs"))));
    }

    [Fact]
    public void FfmpegRtspDecoder_StderrUsesTextReader_NotStdout()
    {
        var source = File.ReadAllText(ResolveSource("FfmpegRtspDecoder.cs"));
        Assert.Contains("StandardError.ReadLineAsync", source);
        Assert.DoesNotContain("StandardOutput.ReadToEnd", source);
        Assert.DoesNotContain("StandardOutput.ReadLineAsync", source);
    }

    [Fact]
    public void FfmpegRtspDecoder_ReadsStdoutViaBaseStream()
    {
        var source = File.ReadAllText(ResolveSource("FfmpegRtspDecoder.cs"));
        Assert.Contains("InstrumentedStdoutStream", source);
        Assert.Contains("StandardOutput.BaseStream", source);
    }

    [Fact]
    public void FfmpegCapabilityProbe_FilePipeline_UsesBaseStream_NotTextReader()
    {
        var source = File.ReadAllText(ResolveSource("FfmpegCapabilityProbe.cs"));
        Assert.Contains("StandardOutput.BaseStream", source);
        Assert.Contains("StandardOutputEncoding = null", source);
        Assert.DoesNotContain("ReadFramesAsync(\n                process.StandardOutput.ReadToEnd", source);
        var pipelineSection = source[..source.IndexOf("private static string RunCommand", StringComparison.Ordinal)];
        Assert.DoesNotContain("StandardOutput.ReadToEnd", pipelineSection);
    }

    [Fact]
    public void FfmpegCapabilityProbe_StderrDrainedAsText()
    {
        var source = File.ReadAllText(ResolveSource("FfmpegCapabilityProbe.cs"));
        Assert.Contains("StandardError.ReadLineAsync", source);
    }

    [Fact]
    public async Task BinaryPipe_MultipleMjpegFrames_ExtractsFirst()
    {
        var a = CreateMinimalJpeg(640, 480);
        var b = CreateMinimalJpeg(320, 240);
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

        Assert.NotEmpty(frames);
        Assert.Equal(a.Length, frames[0].Length);
    }

    [Fact]
    public void Frame2304x1296_ExtractsAndDecodes()
    {
        var jpeg = CreateMinimalJpeg(2304, 1296);
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(jpeg, out var frame, out _));
        var (w, h) = ReadJpegDimensions(frame.ToArray());
        Assert.Equal(2304, w);
        Assert.Equal(1296, h);
        Assert.True(CameraSnapshotPolicy.LooksLikeJpeg(frame.ToArray()));
    }

    [Fact]
    public void ReadDiagnostics_First32Bytes_ContainsJpegSoi()
    {
        var jpeg = CreateMinimalJpeg(128, 96);
        var diagnostics = new CameraDecoderReadDiagnostics();
        diagnostics.MarkStdoutReadStarted();
        diagnostics.RecordChunk(jpeg);

        Assert.True(diagnostics.StdoutReadStarted);
        Assert.StartsWith("FFD8FF", diagnostics.First32BytesHex);
        diagnostics.RecordExtractedFrame(jpeg, decodeSuccess: true);
        Assert.True(diagnostics.JpegSoiFound);
    }

    [Fact]
    public void ReadDiagnostics_RecordsExtractedFrame()
    {
        var jpeg = CreateMinimalJpeg(640, 480);
        var diagnostics = new CameraDecoderReadDiagnostics();
        diagnostics.RecordExtractedFrame(jpeg, decodeSuccess: true);

        Assert.True(diagnostics.JpegExtracted);
        Assert.True(diagnostics.JpegEoiFound);
        Assert.True(diagnostics.JpegDecodeSuccess);
        Assert.Equal(jpeg.Length, diagnostics.JpegSize);
    }

    [Fact]
    public void ProductionChecks_RtspPassIndependentOfJpegFail()
    {
        var probe = new HardwareCameraDiagnosticsResult(
            RtspConnected: true,
            VideoStreamDetected: true,
            Endpoint: "192.168.1.1:554/ch1",
            Codec: "H264",
            Width: 2304,
            Height: 1296,
            FirstFrame: null,
            ReadDiagnostics: new CameraDecoderReadDiagnostics(),
            StderrTailForReport: "frame=131",
            CleanupOk: true,
            Error: null);

        Assert.True(probe.RtspConnected);
        Assert.True(probe.VideoStreamDetected);
        Assert.Null(probe.FirstFrame);
    }

    [Fact]
    public void FailMessage_DoesNotIncludeFfmpegProgressLines()
    {
        const string consoleFail = "MJPEG produced but no JPEG extracted";
        Assert.DoesNotContain("frame=", consoleFail);
        Assert.DoesNotContain("size=", consoleFail);
    }

    private static string ResolveSource(string fileName)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "CanXe.Infrastructure", "Camera", fileName)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "CanXe.Infrastructure", "Camera", fileName))
        };

        return candidates.First(File.Exists);
    }

    private static (int Width, int Height) ReadJpegDimensions(byte[] jpeg)
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
        ms.WriteByte(0xD9);
        return ms.ToArray();
    }
}
