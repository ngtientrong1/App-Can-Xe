namespace CanXe.Application.Models;

public sealed class LatestCameraFrame
{
    public required byte[] JpegBytes { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required Guid CameraSessionId { get; init; }
    public required long FrameSequence { get; init; }

    public LatestCameraFrame Clone() => new()
    {
        JpegBytes = JpegBytes.ToArray(),
        CapturedAt = CapturedAt,
        Width = Width,
        Height = Height,
        CameraSessionId = CameraSessionId,
        FrameSequence = FrameSequence
    };

    public TimeSpan Age(DateTimeOffset now) => now - CapturedAt;
}
