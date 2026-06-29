using CanXe.Application.Models;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class DiagnosticsCliOptions
{
    public bool All { get; set; }
    public bool CameraOnly { get; set; }
    public bool ScaleOnly { get; set; }
    public bool DatabaseOnly { get; set; }
    public bool PublishCheck { get; set; }
    public bool JsonOutput { get; set; }
    public bool Verbose { get; set; }
    public bool ShowHelp { get; set; }
    public bool RequireCamera { get; set; }
    public bool SkipCamera { get; set; }
    public bool RequireScale { get; set; }
    public bool SkipScale { get; set; }
    public bool CaptureCameraPipe { get; set; }
    public int? CameraStabilitySeconds { get; set; }
    public int? ScaleLiveSeconds { get; set; }
    public int? TakeWeightPerformanceCycles { get; set; }
    public bool VerboseScaleFrames { get; set; }
    public bool HasCameraFlagConflict => RequireCamera && SkipCamera;
    public bool HasScaleFlagConflict => RequireScale && SkipScale;
}

public static class DiagnosticsArgumentParser
{
    public static DiagnosticsCliOptions Parse(IReadOnlyList<string> args)
    {
        var options = new DiagnosticsCliOptions();

        foreach (var arg in args)
        {
            if (arg.StartsWith("--camera-stability", StringComparison.OrdinalIgnoreCase))
            {
                options.CameraStabilitySeconds = ParseTrailingInt(arg, args, "--camera-stability", 120);
                continue;
            }

            if (arg.StartsWith("--scale-live", StringComparison.OrdinalIgnoreCase))
            {
                options.ScaleLiveSeconds = ParseTrailingInt(arg, args, "--scale-live", 60);
                continue;
            }

            if (arg.StartsWith("--take-weight-performance", StringComparison.OrdinalIgnoreCase))
            {
                options.TakeWeightPerformanceCycles = ParseTrailingInt(arg, args, "--take-weight-performance", 100);
                continue;
            }

            switch (arg.ToLowerInvariant())
            {
                case "--verbose-scale-frames":
                    options.VerboseScaleFrames = true;
                    break;
                case "--all":
                    options.All = true;
                    break;
                case "--camera":
                    options.CameraOnly = true;
                    break;
                case "--scale":
                    options.ScaleOnly = true;
                    break;
                case "--database":
                    options.DatabaseOnly = true;
                    break;
                case "--publish-check":
                    options.PublishCheck = true;
                    break;
                case "--json":
                    options.JsonOutput = true;
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "--require-camera":
                    options.RequireCamera = true;
                    break;
                case "--skip-camera":
                    options.SkipCamera = true;
                    break;
                case "--require-scale":
                    options.RequireScale = true;
                    break;
                case "--skip-scale":
                    options.SkipScale = true;
                    break;
                case "--capture-camera-pipe":
                    options.CaptureCameraPipe = true;
                    break;
                case "--parse-camera-capture":
                    // Handled in Program.Main before host startup.
                    break;
            }
        }

        if (!options.All && !options.CameraOnly && !options.ScaleOnly && !options.DatabaseOnly && !options.PublishCheck
            && !options.CameraStabilitySeconds.HasValue && !options.ScaleLiveSeconds.HasValue
            && !options.TakeWeightPerformanceCycles.HasValue)
            options.All = true;

        return options;
    }

    private static int ParseTrailingInt(string arg, IReadOnlyList<string> args, string prefix, int defaultValue)
    {
        if (arg.Length > prefix.Length && arg[prefix.Length] == '=')
        {
            var inline = arg[(prefix.Length + 1)..];
            if (int.TryParse(inline, out var seconds) && seconds > 0)
                return seconds;
        }

        var index = Array.IndexOf(args.ToArray(), arg);
        if (index + 1 < args.Count && int.TryParse(args[index + 1], out var next) && next > 0)
            return next;

        return defaultValue;
    }

    private static int ParseCameraStabilitySeconds(string arg, IReadOnlyList<string> args) =>
        ParseTrailingInt(arg, args, "--camera-stability", 120);

    public static DiagnosticsRunOptions BuildRunOptions(DiagnosticsCliOptions cli, string? testMediaPath)
    {
        var skipCamera = cli.SkipCamera;
        var skipScale = cli.SkipScale;
        var requireCamera = cli.RequireCamera && !skipCamera;
        var requireScale = cli.RequireScale && !skipScale;
        var includeCamera = !skipCamera && (cli.All || cli.CameraOnly || cli.PublishCheck);
        var includeScale = !skipScale && (cli.All || cli.ScaleOnly);
        var useTestMedia = cli.PublishCheck
            || (!requireCamera && !skipCamera && !string.IsNullOrWhiteSpace(testMediaPath));

        return new DiagnosticsRunOptions
        {
            IncludeApplication = cli.All,
            IncludeDatabase = cli.All || cli.DatabaseOnly,
            IncludeFfmpeg = cli.All || cli.CameraOnly || cli.PublishCheck,
            IncludeCameraConfig = includeCamera && requireCamera,
            IncludeCameraConnection = includeCamera,
            IncludeScale = includeScale,
            PublishCheck = cli.PublishCheck,
            RequireCamera = requireCamera,
            RequireScale = requireScale,
            SkipCamera = skipCamera,
            SkipScale = skipScale,
            UseTestMedia = useTestMedia,
            CaptureCameraPipe = cli.CaptureCameraPipe,
            TestMediaPath = useTestMedia ? testMediaPath : null
        };
    }
}
