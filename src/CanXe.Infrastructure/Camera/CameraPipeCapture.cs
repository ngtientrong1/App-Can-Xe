namespace CanXe.Infrastructure.Camera;

public static class CameraPipeCapture
{
    public const int MaxCaptureBytes = 2 * 1024 * 1024;

    public static string DefaultCapturePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Diagnostics",
            "camera-first-output.bin");

    public static async Task WriteAsync(Stream source, string path, int maxBytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        var buffer = new byte[64 * 1024];
        var written = 0;

        while (written < maxBytes && !cancellationToken.IsCancellationRequested)
        {
            var toRead = Math.Min(buffer.Length, maxBytes - written);
            var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            written += read;
        }
    }

    public static CameraPipeParseResult ParseFile(string path)
    {
        if (!File.Exists(path))
            return new CameraPipeParseResult(false, 0, 0, 0, 0, 0, $"File not found: {path}");

        var data = File.ReadAllBytes(path);
        var frames = JpegStreamFrameReader.ExtractAllFrames(data);
        if (frames.Count == 0)
            return new CameraPipeParseResult(false, data.Length, 0, 0, 0, 0, "No JPEG frames extracted");

        var first = frames[0];
        var (w, h) = TryReadJpegDimensions(first);
        return new CameraPipeParseResult(
            true,
            data.Length,
            frames.Count,
            first.Length,
            w,
            h,
            $"{frames.Count} frame(s); first={first.Length / 1024} KB {w}×{h}");
    }

    private static (int Width, int Height) TryReadJpegDimensions(byte[] jpeg)
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
}

public sealed record CameraPipeParseResult(
    bool Success,
    int FileBytes,
    int FrameCount,
    int FirstFrameBytes,
    int Width,
    int Height,
    string Detail);
