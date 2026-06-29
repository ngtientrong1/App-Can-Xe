namespace CanXe.Domain.Services;

public static class CameraSnapshotPolicy
{
    public const int MinValidFileBytes = 512;
    public const int MinValidWidth = 2;
    public const int MinValidHeight = 2;
    public const int SnapshotMinWidth = 320;
    public const int SnapshotMinHeight = 180;
    public const int SnapshotMaximumFrameAgeSeconds = 3;
    public static readonly TimeSpan MaxFreshFrameAge = TimeSpan.FromSeconds(SnapshotMaximumFrameAgeSeconds);
    public static readonly TimeSpan SnapshotWaitForFreshFrameTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan StaleFrameAge = TimeSpan.FromSeconds(10);

    public static bool IsValidFileSize(long byteCount) => byteCount >= MinValidFileBytes;

    public static bool IsValidDimensions(int width, int height) =>
        width >= MinValidWidth && height >= MinValidHeight;

    public static bool IsSnapshotDimensions(int width, int height) =>
        width > SnapshotMinWidth && height > SnapshotMinHeight;

    public static bool IsFreshFrame(DateTimeOffset capturedAt, DateTimeOffset now) =>
        now - capturedAt <= MaxFreshFrameAge;

    public static bool IsSnapshotFresh(DateTimeOffset capturedAt, DateTimeOffset now) =>
        IsFreshFrame(capturedAt, now);

    public static bool LooksLikeJpeg(byte[] bytes) =>
        bytes.Length >= 4
        && bytes[0] == 0xFF
        && bytes[1] == 0xD8
        && bytes[^2] == 0xFF
        && bytes[^1] == 0xD9;
}
