namespace CanXe.Application.Models;

public sealed class BuildInfo
{
    public required string Version { get; init; }
    public required string BuildTimestamp { get; init; }
    public required string GitCommit { get; init; }
    public required string Configuration { get; init; }
    public required string DeviceMode { get; init; }
    public required string AppBaseDirectory { get; init; }

    public string ToDisplayText() =>
        string.Join(Environment.NewLine,
            $"Version: {Version}",
            $"Build: {BuildTimestamp}",
            $"Commit: {GitCommit}",
            $"Configuration: {Configuration}",
            $"Mode: {DeviceMode}",
            $"App folder: {AppBaseDirectory}");
}
