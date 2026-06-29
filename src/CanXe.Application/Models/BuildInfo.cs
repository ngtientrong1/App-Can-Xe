namespace CanXe.Application.Models;

public sealed class BuildInfo
{
    public string Version { get; init; } = "0.0.0";
    public string BuildTimestamp { get; init; } = "—";
    public string GitCommit { get; init; } = "—";
    public string Configuration { get; init; } = "Release";
    public string DeviceMode { get; init; } = "Simulation";
    public string AppBaseDirectory { get; init; } = string.Empty;
    public string CameraDecoderName { get; init; } = "—";
    public string FfmpegPath { get; init; } = "—";
    public string FfmpegStatus { get; init; } = "Not found";

    public string ToDisplayText() =>
        string.Join(Environment.NewLine,
            $"Version: {Version}",
            $"Build: {BuildTimestamp}",
            $"Commit: {GitCommit}",
            $"Configuration: {Configuration}",
            $"Mode: {DeviceMode}",
            $"Decoder: {CameraDecoderName}",
            $"App folder: {AppBaseDirectory}",
            $"FFmpeg: {FfmpegStatus}");
}
