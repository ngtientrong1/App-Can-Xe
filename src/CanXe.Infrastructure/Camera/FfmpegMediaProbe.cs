using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CanXe.Infrastructure.Camera;

public sealed class FfmpegMediaInfo
{
    public int VideoWidth { get; init; }
    public int VideoHeight { get; init; }
    public double DurationSeconds { get; init; }
    public IReadOnlyList<string> StreamCodecTypes { get; init; } = [];
    public string? VideoCodec { get; init; }
}

public static partial class FfmpegMediaProbe
{
    private static readonly Regex DurationHmsRegex = DurationHmsPattern();
    private static readonly Regex DurationDecimalRegex = DurationDecimalPattern();
    private static readonly Regex VideoCodecRegex = VideoCodecPattern();
    private static readonly Regex VideoSizeRegex = VideoSizePattern();

    public static FfmpegMediaInfo? Probe(string mediaPath, string? ffprobePath = null)
    {
        if (!File.Exists(mediaPath))
            return null;

        var probeExecutable = ResolveProbeExecutable(ffprobePath);
        if (probeExecutable is null)
            return null;

        var output = RunCommand(probeExecutable, $"-hide_banner -i \"{mediaPath}\"");
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var streams = new List<string>();
        string? videoCodec = null;
        var width = 0;
        var height = 0;

        foreach (var line in output.Split('\n'))
        {
            if (line.Contains("Audio:", StringComparison.OrdinalIgnoreCase))
            {
                streams.Add("audio");
                continue;
            }

            if (!line.Contains("Video:", StringComparison.OrdinalIgnoreCase))
                continue;

            streams.Add("video");
            var codecMatch = VideoCodecRegex.Match(line);
            if (codecMatch.Success)
                videoCodec = codecMatch.Groups[1].Value;

            var sizeMatch = VideoSizeRegex.Match(line);
            if (sizeMatch.Success)
            {
                width = int.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                height = int.Parse(sizeMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            }
        }

        var duration = ParseDuration(output);

        return new FfmpegMediaInfo
        {
            VideoWidth = width,
            VideoHeight = height,
            DurationSeconds = duration,
            StreamCodecTypes = streams,
            VideoCodec = videoCodec
        };
    }

    public static string? ResolveFfprobe() => ResolveProbeExecutable(null);

    private static string? ResolveProbeExecutable(string? preferredPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
            return preferredPath;

        var baseDir = AppContext.BaseDirectory;
        var bundledProbe = Path.Combine(baseDir, "ffmpeg", "ffprobe.exe");
        if (File.Exists(bundledProbe))
            return bundledProbe;

        var ffmpegPath = FfmpegPathResolver.ResolveFfmpegExecutable();
        if (ffmpegPath is not null)
        {
            var siblingProbe = Path.Combine(Path.GetDirectoryName(ffmpegPath)!, "ffprobe.exe");
            if (File.Exists(siblingProbe))
                return siblingProbe;
            if (File.Exists(ffmpegPath))
                return ffmpegPath;
        }

        var repoRoot = FindRepoRoot();
        if (repoRoot is not null)
        {
            var thirdPartyProbe = Path.Combine(repoRoot, "third-party", "ffmpeg", "win-x64", "ffprobe.exe");
            if (File.Exists(thirdPartyProbe))
                return thirdPartyProbe;

            var thirdPartyFfmpeg = Path.Combine(repoRoot, "third-party", "ffmpeg", "win-x64", "ffmpeg.exe");
            if (File.Exists(thirdPartyFfmpeg))
                return thirdPartyFfmpeg;
        }

        return null;
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CanXe.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    private static string RunCommand(string executable, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return string.Empty;

            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);
            return string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static double ParseDuration(string output)
    {
        var hmsMatch = DurationHmsRegex.Match(output);
        if (hmsMatch.Success)
        {
            var hours = int.Parse(hmsMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(hmsMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            var seconds = double.Parse(hmsMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            return hours * 3600 + minutes * 60 + seconds;
        }

        var decimalMatch = DurationDecimalRegex.Match(output);
        if (decimalMatch.Success
            && double.TryParse(decimalMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var secondsOnly))
        {
            return secondsOnly;
        }

        return 0;
    }

    [GeneratedRegex(@"Duration:\s*(\d{2}):(\d{2}):(\d{2}(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex DurationHmsPattern();

    [GeneratedRegex(@"Duration:\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex DurationDecimalPattern();

    [GeneratedRegex(@"Video:\s*(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex VideoCodecPattern();

    [GeneratedRegex(@"(\d{2,5})x(\d{2,5})", RegexOptions.IgnoreCase)]
    private static partial Regex VideoSizePattern();
}
