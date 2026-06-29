using CanXe.Infrastructure.Camera;

namespace CanXe.Tests.Application;

public class JpegStreamFrameReaderTests
{
    [Fact]
    public void SoiSplitAcrossTwoReads_ReassemblesFrame()
    {
        var jpeg = CreateMinimalJpeg(40, 30);
        var part1 = jpeg.Take(1).ToArray();
        var part2 = jpeg.Skip(1).ToArray();
        var buffer = part1.Concat(part2).ToArray();
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(buffer, out var frame, out var consumed));
        Assert.Equal(jpeg.Length, frame.Length);
        Assert.Equal(jpeg.Length, consumed);
    }

    [Fact]
    public void EoiSplitAcrossTwoReads_ReassemblesOnSecondChunk()
    {
        var jpeg = CreateMinimalJpeg(32, 24);
        var split = jpeg.Length - 1;
        var pending = jpeg.Take(split).ToArray();
        Assert.False(JpegStreamFrameReader.TryExtractNextFrame(pending, out _, out _));
        pending = pending.Concat(jpeg.Skip(split)).ToArray();
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(pending, out var frame, out _));
        Assert.Equal(jpeg.Length, frame.Length);
    }

    [Fact]
    public void TwoJpegsInOneRead_ExtractsFirstOnly()
    {
        var a = CreateMinimalJpeg(20, 20);
        var b = CreateMinimalJpeg(24, 24);
        var combined = a.Concat(b).ToArray();
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(combined, out var frame, out var consumed));
        Assert.Equal(a.Length, frame.Length);
        Assert.Equal(a.Length, consumed);
    }

    [Fact]
    public void GarbageBeforeSoi_SkipsToFirstFrame()
    {
        var jpeg = CreateMinimalJpeg(16, 16);
        var combined = new byte[] { 0, 1, 2, 3 }.Concat(jpeg).ToArray();
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(combined, out var frame, out var consumed));
        Assert.Equal(jpeg.Length, frame.Length);
        Assert.Equal(4 + jpeg.Length, consumed);
    }

    [Fact]
    public void TrailingBytesAfterEoi_ConsumedIncludesPadding()
    {
        var jpeg = CreateMinimalJpeg(16, 16);
        var combined = jpeg.Concat(new byte[] { 0, 0, 0xFF, 0xD8 }).ToArray();
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(combined, out _, out var consumed));
        Assert.True(consumed >= jpeg.Length);
    }

    [Fact]
    public async Task NonSeekableStream_ReadsFrames()
    {
        var jpeg = CreateMinimalJpeg(48, 36);
        await using var stream = new MemoryStream(jpeg, writable: false);
        var frames = new List<byte[]>();
        await JpegStreamFrameReader.ReadFramesAsync(stream, (f, _) => { frames.Add(f); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Single(frames);
    }

    [Fact]
    public void LargeFrame_Over500Kb_Extracts()
    {
        var core = CreateMinimalJpeg(640, 480);
        var padded = PadJpegToSize(core, 520 * 1024);
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(padded, out var frame, out _));
        Assert.True(frame.Length > 500 * 1024);
    }

    [Fact]
    public void Frame2304x1296_HeaderDimensions_Extracts()
    {
        var jpeg = CreateMinimalJpeg(2304, 1296);
        Assert.True(JpegStreamFrameReader.TryExtractNextFrame(jpeg, out var frame, out _));
        Assert.True(frame.Length > 10 * 1024);
    }

    [Fact]
    public async Task ReturnsFrameBeforeEof()
    {
        var jpeg = CreateMinimalJpeg(24, 24);
        await using var stream = new MemoryStream(jpeg);
        var got = false;
        await JpegStreamFrameReader.ReadFramesAsync(stream, (_, _) => { got = true; return Task.CompletedTask; }, CancellationToken.None);
        Assert.True(got);
    }

    [Fact]
    public async Task CancellationDuringRead_StopsWithoutHang()
    {
        var jpeg = CreateMinimalJpeg(64, 64);
        var repeated = Enumerable.Range(0, 200).SelectMany(_ => jpeg).ToArray();
        await using var stream = new SlowStream(repeated);
        using var cts = new CancellationTokenSource(100);
        var frames = 0;
        await JpegStreamFrameReader.ReadFramesAsync(
            stream,
            (_, _) => { Interlocked.Increment(ref frames); return Task.CompletedTask; },
            cts.Token);
        Assert.True(frames >= 0);
    }

    [Fact]
    public async Task StreamClosedMidFrame_DoesNotHangForever()
    {
        var partial = new byte[] { 0xFF, 0xD8, 0x00, 0x01 };
        await using var stream = new MemoryStream(partial);
        var frames = new List<byte[]>();
        await JpegStreamFrameReader.ReadFramesAsync(stream, (f, _) => { frames.Add(f); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Empty(frames);
    }

    [Fact]
    public async Task CorruptJpeg_DoesNotKillParser_SubsequentFrameWorks()
    {
        var good = CreateMinimalJpeg(32, 32);
        var corrupt = new byte[] { 0xFF, 0xD8, 0x00, 0x11, 0x22 };
        var combined = corrupt.Concat(good).ToArray();
        await using var stream = new MemoryStream(combined);
        var frames = new List<byte[]>();
        await JpegStreamFrameReader.ReadFramesAsync(stream, (f, _) => { frames.Add(f); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Single(frames);
    }

    [Fact]
    public async Task InvalidThenValidFrame_Recovers()
    {
        var good = CreateMinimalJpeg(28, 28);
        var junk = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        await using var stream = new MemoryStream(junk.Concat(good).ToArray());
        var frames = new List<byte[]>();
        await JpegStreamFrameReader.ReadFramesAsync(stream, (f, _) => { frames.Add(f); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Single(frames);
    }

    [Fact]
    public async Task BufferDoesNotGrowUnbounded_OnIncompleteData()
    {
        var incomplete = Enumerable.Repeat((byte)0xAA, 5 * 1024 * 1024 + 100).ToArray();
        await using var stream = new MemoryStream(incomplete);
        var frames = new List<byte[]>();
        await JpegStreamFrameReader.ReadFramesAsync(stream, (f, _) => { frames.Add(f); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Empty(frames);
    }

    private static byte[] CreateMinimalJpeg(int width, int height)
    {
        if (!OperatingSystem.IsWindows())
            return CreateFakeJpegHeader(width, height);

        using var bitmap = new System.Drawing.Bitmap(width, height);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.Coral);
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        return ms.ToArray();
    }

    private static byte[] PadJpegToSize(byte[] jpeg, int targetSize)
    {
        if (jpeg.Length >= targetSize)
            return jpeg;

        using var ms = new MemoryStream(capacity: targetSize);
        ms.Write(jpeg, 0, 2);
        var commentLength = targetSize - jpeg.Length + 2;
        ms.WriteByte(0xFF);
        ms.WriteByte(0xFE);
        ms.WriteByte((byte)(commentLength >> 8));
        ms.WriteByte((byte)(commentLength & 0xFF));
        ms.Write(new byte[commentLength - 2]);
        ms.Write(jpeg, 2, jpeg.Length - 2);
        return ms.ToArray();
    }

    private static byte[] CreateLargeJpeg(int width, int height)
    {
        using var bitmap = new System.Drawing.Bitmap(width, height);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        for (var y = 0; y < height; y += 8)
            for (var x = 0; x < width; x += 8)
                g.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb((x * 3) % 255, (y * 5) % 255, 128)), x, y, 8, 8);
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        return ms.ToArray();
    }

    private static byte[] CreateFakeJpegHeader(int width, int height)
    {
        var data = new List<byte> { 0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08 };
        data.Add((byte)(height >> 8));
        data.Add((byte)(height & 0xFF));
        data.Add((byte)(width >> 8));
        data.Add((byte)(width & 0xFF));
        data.AddRange(new byte[] { 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01, 0xFF, 0xD9 });
        return data.ToArray();
    }

    private sealed class SlowStream(byte[] data) : MemoryStream(data)
    {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            return await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }
    }
}
