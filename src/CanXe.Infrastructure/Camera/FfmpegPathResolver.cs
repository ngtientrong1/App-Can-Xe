namespace CanXe.Infrastructure.Camera;

public static class FfmpegPathResolver
{
    public static string? ResolveFfmpegExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        var bundled = Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(bundled))
            return bundled;

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        foreach (var entry in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(entry, "ffmpeg.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string DescribeResolvedPath() =>
        ResolveFfmpegExecutable() ?? "ffmpeg.exe (not found)";
}
