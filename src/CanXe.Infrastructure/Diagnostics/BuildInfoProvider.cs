using System.Diagnostics;
using System.Reflection;
using System.Text;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Infrastructure.Camera;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class BuildInfoProvider(
    AppSettings appSettings,
    ICameraDecoderFactory decoderFactory) : IBuildInfoProvider
{
    public BuildInfo GetBuildInfo()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        var buildTime = GetBuildTimestamp(assembly);
        var gitCommit = GetGitCommit();
        var ffmpegPath = FfmpegPathResolver.ResolveFfmpegExecutable();
        var decoder = decoderFactory.CreateDecoder();

        return new BuildInfo
        {
            Version = version,
            BuildTimestamp = buildTime,
            GitCommit = gitCommit,
            Configuration = GetConfiguration(),
            DeviceMode = appSettings.DeviceMode,
            AppBaseDirectory = AppContext.BaseDirectory,
            CameraDecoderName = decoder.GetType().Name,
            FfmpegPath = ffmpegPath ?? "—",
            FfmpegStatus = ffmpegPath is not null && File.Exists(ffmpegPath)
                ? DescribeFfmpegVersion(ffmpegPath)
                : "Not found"
        };
    }

    private static string GetBuildTimestamp(Assembly assembly)
    {
        var path = assembly.Location;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm");

        return DateTime.Now.ToString("yyyy-MM-dd HH:mm");
    }

    private static string GetGitCommit()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return "unknown";

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2000);
            return string.IsNullOrWhiteSpace(output) ? "unknown" : output;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string DescribeFfmpegVersion(string ffmpegPath)
    {
        try
        {
            var version = FfmpegCapabilityProbe.GetVersion(ffmpegPath);
            return string.IsNullOrWhiteSpace(version) ? "Found" : version;
        }
        catch
        {
            return "Found";
        }
    }
}
