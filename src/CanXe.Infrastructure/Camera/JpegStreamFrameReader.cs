using System.Runtime.InteropServices;

namespace CanXe.Infrastructure.Camera;

public static class JpegStreamFrameReader
{
    public const int ReadChunkBytes = 256 * 1024;
    public const int MaxFrameBytes = 16 * 1024 * 1024;

    public static bool TryExtractNextFrame(ReadOnlySpan<byte> buffer, out ReadOnlySpan<byte> frame, out int consumed)
    {
        frame = default;
        consumed = 0;
        if (buffer.Length < 4)
            return false;

        var start = IndexOf(buffer, [0xFF, 0xD8]);
        if (start < 0)
            return false;

        var end = FindMarkerAwareEoiEnd(buffer, start);
        if (end < 0)
            return false;

        frame = buffer[start..end];
        consumed = end;
        while (consumed < buffer.Length && buffer[consumed] == 0)
            consumed++;

        return frame.Length > 4;
    }

    public static async Task ReadFramesAsync(
        Stream stream,
        Func<byte[], CancellationToken, Task> onFrame,
        CancellationToken cancellationToken)
    {
        var extractor = new MjpegPipeFrameExtractor();
        var readBuffer = new byte[ReadChunkBytes];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (read <= 0)
                break;

            foreach (var frame in extractor.Feed(readBuffer.AsSpan(0, read)))
            {
                await onFrame(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static IReadOnlyList<byte[]> ExtractAllFrames(ReadOnlySpan<byte> data)
    {
        var extractor = new MjpegPipeFrameExtractor();
        return extractor.Feed(data).ToList();
    }

    internal static int FindMarkerAwareEoiEnd(ReadOnlySpan<byte> data, int soiIndex)
    {
        if (soiIndex < 0 || data.Length < soiIndex + 4)
            return -1;

        if (data[soiIndex] != 0xFF || data[soiIndex + 1] != 0xD8)
            return -1;

        var i = soiIndex + 2;
        while (i < data.Length - 1)
        {
            if (data[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = data[i + 1];
            if (marker == 0xD9)
                return i + 2;

            if (marker == 0xDA)
            {
                i += 2;
                return FindEoiInScanData(data, i);
            }

            if (marker is 0x00 or 0x01 or >= 0xD0 and <= 0xD7)
            {
                i += 2;
                continue;
            }

            if (i + 3 >= data.Length)
                return -1;

            var segmentLength = (data[i + 2] << 8) | data[i + 3];
            if (segmentLength < 2)
                return -1;

            i += 2 + segmentLength;
        }

        return -1;
    }

    private static int FindEoiInScanData(ReadOnlySpan<byte> data, int start)
    {
        for (var i = start; i < data.Length - 1; i++)
        {
            if (data[i] != 0xFF)
                continue;

            var next = data[i + 1];
            if (next == 0x00)
            {
                i++;
                continue;
            }

            if (next == 0xD9)
                return i + 2;

            if (next is >= 0xD0 and <= 0xD7)
            {
                i++;
                continue;
            }
        }

        return -1;
    }

    internal static int IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> pattern)
    {
        if (pattern.Length == 0 || source.Length < pattern.Length)
            return -1;

        for (var i = 0; i <= source.Length - pattern.Length; i++)
        {
            if (source.Slice(i, pattern.Length).SequenceEqual(pattern))
                return i;
        }

        return -1;
    }
}

internal enum MjpegParseState
{
    SearchingForSoi,
    ReadingJpeg
}

internal sealed class MjpegPipeFrameExtractor
{
    private readonly List<byte> _leftover = new(JpegStreamFrameReader.ReadChunkBytes);
    private MjpegParseState _state = MjpegParseState.SearchingForSoi;
    private int _partialSoiIndex = -1;

    public IEnumerable<byte[]> Feed(ReadOnlySpan<byte> chunk)
    {
        if (chunk.IsEmpty)
            return [];

        _leftover.AddRange(chunk.ToArray());
        return DrainFrames();
    }

    private IEnumerable<byte[]> DrainFrames()
    {
        while (true)
        {
            if (_state == MjpegParseState.SearchingForSoi)
            {
                if (!TryEnterReadingJpeg())
                    yield break;
            }

            if (_state != MjpegParseState.ReadingJpeg)
                yield break;

            if (_leftover.Count > JpegStreamFrameReader.MaxFrameBytes)
            {
                ResetToSearching("frame too large");
                continue;
            }

            var end = JpegStreamFrameReader.FindMarkerAwareEoiEnd(CollectionsMarshal.AsSpan(_leftover), 0);
            if (end < 0)
            {
                var span = CollectionsMarshal.AsSpan(_leftover);
                if (span.Length > 2)
                {
                    var nextSoi = JpegStreamFrameReader.IndexOf(span[2..], [0xFF, 0xD8]);
                    if (nextSoi >= 0)
                    {
                        _leftover.RemoveRange(0, nextSoi + 2);
                        continue;
                    }
                }

                yield break;
            }

            var frame = _leftover.GetRange(0, end).ToArray();
            var consumed = end;
            while (consumed < _leftover.Count && _leftover[consumed] == 0)
                consumed++;

            _leftover.RemoveRange(0, consumed);
            _state = MjpegParseState.SearchingForSoi;
            _partialSoiIndex = -1;

            if (frame.Length > 4)
                yield return frame;
        }
    }

    private bool TryEnterReadingJpeg()
    {
        var span = CollectionsMarshal.AsSpan(_leftover);
        var soi = JpegStreamFrameReader.IndexOf(span, [0xFF, 0xD8]);
        if (soi < 0)
        {
            if (_leftover.Count > 1)
            {
                var keep = _leftover[^1] == 0xFF ? 1 : 0;
                if (keep == 0)
                    _leftover.Clear();
                else
                {
                    var last = _leftover[^1];
                    _leftover.Clear();
                    _leftover.Add(last);
                }
            }

            return false;
        }

        if (soi > 0)
            _leftover.RemoveRange(0, soi);

        _state = MjpegParseState.ReadingJpeg;
        _partialSoiIndex = 0;
        return true;
    }

    private void ResetToSearching(string _)
    {
        _leftover.Clear();
        _state = MjpegParseState.SearchingForSoi;
        _partialSoiIndex = -1;
    }
}
