using CanXe.Application.Models;

namespace CanXe.Infrastructure.Diagnostics;

public sealed class DiagnosticsCliOptions
{
    public bool All { get; set; }
    public bool ScaleOnly { get; set; }
    public bool DatabaseOnly { get; set; }
    public bool PublishCheck { get; set; }
    public bool JsonOutput { get; set; }
    public bool Verbose { get; set; }
    public bool ShowHelp { get; set; }
    public bool RequireScale { get; set; }
    public bool SkipScale { get; set; }
    public int? ScaleLiveSeconds { get; set; }
    public int? TakeWeightPerformanceCycles { get; set; }
    public bool VerboseScaleFrames { get; set; }
    public bool PrintRenderTest { get; set; }
    public bool PrintRenderSendToPrinter { get; set; }
    public bool PrintLayoutGeometryTest { get; set; }
    public bool PrintA5FitTest { get; set; }
    public bool PrintA5FitSendToPrinter { get; set; }
    public bool PrintA4TwoUpTest { get; set; }
    public bool HasScaleFlagConflict => RequireScale && SkipScale;
}

public static class DiagnosticsArgumentParser
{
    public static DiagnosticsCliOptions Parse(IReadOnlyList<string> args)
    {
        var options = new DiagnosticsCliOptions();

        foreach (var arg in args)
        {
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
                case "--require-scale":
                    options.RequireScale = true;
                    break;
                case "--skip-scale":
                    options.SkipScale = true;
                    break;
                case "--print-layout-geometry-test":
                    options.PrintLayoutGeometryTest = true;
                    break;
                case "--print-a5-fit-test":
                    options.PrintA5FitTest = true;
                    break;
                case "--print-a4-two-up-test":
                    options.PrintA4TwoUpTest = true;
                    break;
                case "--print-render-test":
                    options.PrintRenderTest = true;
                    break;
                case "--send-to-printer":
                    options.PrintRenderSendToPrinter = true;
                    break;
            }
        }

        if (!options.All && !options.ScaleOnly && !options.DatabaseOnly && !options.PublishCheck
            && !options.ScaleLiveSeconds.HasValue && !options.TakeWeightPerformanceCycles.HasValue
            && !options.PrintRenderTest && !options.PrintLayoutGeometryTest && !options.PrintA5FitTest
            && !options.PrintA4TwoUpTest)
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

    public static DiagnosticsRunOptions BuildRunOptions(DiagnosticsCliOptions cli)
    {
        var skipScale = cli.SkipScale;
        var requireScale = cli.RequireScale && !skipScale;
        var includeScale = !skipScale && (cli.All || cli.ScaleOnly || cli.PublishCheck);

        return new DiagnosticsRunOptions
        {
            IncludeApplication = cli.All || cli.PublishCheck,
            IncludeDatabase = cli.All || cli.DatabaseOnly,
            IncludeFilesystem = cli.All || cli.PublishCheck,
            IncludePrinter = cli.All || cli.PublishCheck,
            IncludeScale = includeScale,
            PublishCheck = cli.PublishCheck,
            RequireScale = requireScale,
            SkipScale = skipScale
        };
    }
}
