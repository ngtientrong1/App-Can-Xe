using System.Diagnostics;
using System.Reflection;
using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class BuildInfoProvider(AppSettings appSettings) : IBuildInfoProvider
{
    public BuildInfo GetBuildInfo()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        // Strip optional SourceRevision suffix if present (e.g. 1.0.0+abcdef).
        var plus = version.IndexOf('+');
        if (plus >= 0)
            version = version[..plus];

        return new BuildInfo
        {
            Version = version,
            AppName = "Cân Xe Tiến Trọng",
            BuildTimestamp = GetBuildTimestamp(assembly),
            GitCommit = GetGitCommit(),
            Configuration = GetConfiguration(),
            DeviceMode = appSettings.DeviceMode,
            AppBaseDirectory = AppContext.BaseDirectory
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
}
