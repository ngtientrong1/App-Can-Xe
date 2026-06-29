namespace CanXe.Infrastructure.Camera;

public sealed class CameraDecoderReadDiagnostics
{
    public bool StdoutReadStarted { get; private set; }
    public long StdoutBytesRead { get; private set; }
    public string? First32BytesHex { get; private set; }
    public bool JpegSoiFound { get; private set; }
    public bool JpegEoiFound { get; private set; }
    public bool JpegExtracted { get; private set; }
    public int JpegSize { get; private set; }
    public bool JpegDecodeSuccess { get; private set; }
    public int FrameTooLargeEvents { get; private set; }

    public void MarkStdoutReadStarted() => StdoutReadStarted = true;

    public void RecordChunk(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length == 0)
            return;

        StdoutBytesRead += chunk.Length;

        if (First32BytesHex is null)
        {
            var take = Math.Min(32, chunk.Length);
            First32BytesHex = Convert.ToHexString(chunk[..take]);
        }
    }

    public void RecordExtractedFrame(byte[] jpeg, bool decodeSuccess)
    {
        JpegExtracted = true;
        JpegSize = jpeg.Length;
        JpegSoiFound = jpeg.Length >= 2 && jpeg[0] == 0xFF && jpeg[1] == 0xD8;
        JpegEoiFound = jpeg.Length >= 2 && jpeg[^2] == 0xFF && jpeg[^1] == 0xD9;
        JpegDecodeSuccess = decodeSuccess;
    }

    public void RecordFrameTooLarge(int bytes) =>
        FrameTooLargeEvents++;

    public string Summarize() =>
        $"StdoutReadStarted={StdoutReadStarted}; StdoutBytesRead={StdoutBytesRead}; " +
        $"First32BytesHex={First32BytesHex ?? "none"}; JpegSoiFound={JpegSoiFound}; " +
        $"JpegEoiFound={JpegEoiFound}; JpegExtracted={JpegExtracted}; JpegSize={JpegSize}; " +
        $"JpegDecodeSuccess={JpegDecodeSuccess}; FrameTooLargeEvents={FrameTooLargeEvents}";
}

internal sealed class InstrumentedStdoutStream : Stream
{
    private readonly Stream _inner;
    private readonly CameraDecoderReadDiagnostics _diagnostics;
    private readonly FileStream? _capture;
    private int _capturedBytes;

    public InstrumentedStdoutStream(
        Stream inner,
        CameraDecoderReadDiagnostics diagnostics,
        string? capturePath = null,
        int maxCaptureBytes = CameraPipeCapture.MaxCaptureBytes)
    {
        _inner = inner;
        _diagnostics = diagnostics;
        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
            _capture = new FileStream(capturePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _capturedBytes = 0;
        }
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        if (read > 0)
            RecordRead(buffer.AsSpan(offset, read));
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        if (read > 0)
            RecordRead(buffer.AsSpan(offset, read));
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > 0)
            RecordRead(buffer.Span[..read]);
        return read;
    }

    private void RecordRead(ReadOnlySpan<byte> chunk)
    {
        _diagnostics.RecordChunk(chunk);
        if (_capture is null || _capturedBytes >= CameraPipeCapture.MaxCaptureBytes)
            return;

        var toWrite = Math.Min(chunk.Length, CameraPipeCapture.MaxCaptureBytes - _capturedBytes);
        _capture.Write(chunk[..toWrite]);
        _capturedBytes += toWrite;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _capture?.Dispose();
        base.Dispose(disposing);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
