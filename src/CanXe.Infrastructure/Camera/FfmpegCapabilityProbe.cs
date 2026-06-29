using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CanXe.Infrastructure.Camera;

public static partial class FfmpegCapabilityProbe
{
    private static readonly Regex VersionRegex = VersionPattern();

    public static string? GetVersion(string ffmpegPath)
    {
        var output = RunCommand(ffmpegPath, "-version");
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var match = VersionRegex.Match(output);
        return match.Success ? match.Groups[1].Value : output.Split('\n')[0].Trim();
    }

    public static bool SupportsRtspTimeoutOption(string ffmpegPath)
    {
        var help = RunCommand(ffmpegPath, "-h demuxer=rtsp");
        var legacyStimeout = "-s" + "timeout";
        return help.Contains("-timeout", StringComparison.Ordinal)
            && !help.Contains(legacyStimeout, StringComparison.Ordinal);
    }

    public static bool VersionCommandSucceeds(string ffmpegPath) =>
        !string.IsNullOrWhiteSpace(GetVersion(ffmpegPath));

    public static string BuildFileInputArguments(string inputPath) =>
        string.Join(
            ' ',
            "-hide_banner",
            "-nostdin",
            "-loglevel info",
            $"-i \"{inputPath}\"",
            "-map 0:v:0",
            "-an",
            "-sn",
            "-dn",
            "-vf fps=12",
            "-c:v mjpeg",
            "-q:v 5",
            "-f image2pipe",
            "pipe:1");

    public static async Task<FfmpegPipelineResult> RunFilePipelineAsync(
        string ffmpegPath,
        string inputPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var args = BuildFileInputArguments(inputPath);
        return await RunPipelineAsync(ffmpegPath, args, timeout, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<FfmpegPipelineResult> RunPipelineAsync(
        string ffmpegPath,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                CreateNoWindow = true,
                StandardOutputEncoding = null,
                StandardErrorEncoding = null
            },
            EnableRaisingEvents = true
        };

        var stderr = new StringBuilder();
        var frames = new List<byte[]>();
        var stdoutBytes = 0L;

        process.Start();
        var stderrTask = Task.Run(async () =>
        {
            try
            {
                while (!process.HasExited && !cts.Token.IsCancellationRequested)
                {
                    var line = await process.StandardError.ReadLineAsync(cts.Token).ConfigureAwait(false);
                    if (line is null)
                        break;
                    stderr.AppendLine(line);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }, cts.Token);

        try
        {
            await JpegStreamFrameReader.ReadFramesAsync(
                process.StandardOutput.BaseStream,
                (jpeg, _) =>
                {
                    stdoutBytes += jpeg.Length;
                    frames.Add(jpeg);
                    return Task.CompletedTask;
                },
                cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on timeout.
        }

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await Task.WhenAny(stderrTask, Task.Delay(500)).ConfigureAwait(false);
        var exitCode = process.HasExited ? process.ExitCode : -1;
        process.Dispose();

        return new FfmpegPipelineResult
        {
            ExitCode = exitCode,
            StdoutBytes = stdoutBytes,
            Frames = frames,
            StderrTail = GetStderrTail(stderr.ToString(), 20)
        };
    }

    public static int CountFfmpegProcesses()
    {
        try
        {
            return Process.GetProcessesByName("ffmpeg").Length;
        }
        catch
        {
            return 0;
        }
    }

    public static string GetStderrTail(string stderr, int lineCount)
    {
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(Environment.NewLine, lines.TakeLast(lineCount));
    }

    private static string RunCommand(string ffmpegPath, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
            return string.IsNullOrWhiteSpace(output) ? error : output;
        }
        catch
        {
            return string.Empty;
        }
    }

    [GeneratedRegex(@"ffmpeg version\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();
}

public sealed class FfmpegPipelineResult
{
    public int ExitCode { get; init; }
    public long StdoutBytes { get; init; }
    public IReadOnlyList<byte[]> Frames { get; init; } = [];
    public string StderrTail { get; init; } = string.Empty;
    public bool HasFrames => Frames.Count > 0;
}
