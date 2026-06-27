namespace CanXe.DeviceTester.Core.Models;

public enum DeviceTesterCaptureMode
{
    Manual,
    Guided
}

public enum GuidedSessionType
{
    EmptyStable,
    PersonStable,
    EmptyPersonTransition
}

public enum CaptureQualityStatus
{
    Valid,
    Warning,
    Invalid
}

public sealed class CaptureEventMarker
{
    public required string EventType { get; init; }
    public required string Label { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required double ElapsedMilliseconds { get; init; }
    public required long RawByteOffset { get; init; }
    public required long ChunkNumber { get; init; }
}

public static class CaptureEventTypes
{
    public const string CaptureStarted = "CaptureStarted";
    public const string EmptyStableStarted = "EmptyStableStarted";
    public const string PersonStepOnStarted = "PersonStepOnStarted";
    public const string PersonStableStarted = "PersonStableStarted";
    public const string PersonStepOffStarted = "PersonStepOffStarted";
    public const string EmptyRestored = "EmptyRestored";
    public const string CaptureStopped = "CaptureStopped";
    public const string UserNote = "UserNote";

    public static readonly IReadOnlyList<string> TransitionRequired =
    [
        PersonStepOnStarted,
        PersonStableStarted,
        PersonStepOffStarted,
        EmptyRestored
    ];
}

public sealed class CaptureSaveOptions
{
    public required SerialPortSettings PortSettings { get; init; }
    public GuidedSessionType? SessionType { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public IReadOnlyList<CaptureEventMarker>? Events { get; init; }
    public int TotalChunks { get; init; }
    public IReadOnlyList<byte>? UniqueByteValues { get; init; }
    public double LongestNoDataGapMs { get; init; }
    public bool PortErrorDuringCapture { get; init; }
    public DateTimeOffset? RecordingStartedAt { get; init; }
    public DateTimeOffset? RecordingEndedAt { get; init; }
}

public sealed record CaptureRecordingStats
{
    public TimeSpan Duration { get; init; }
    public int TotalBytes { get; init; }
    public int TotalChunks { get; init; }
    public double BytesPerSecond { get; init; }
    public IReadOnlyList<byte> UniqueByteValues { get; init; } = [];
    public double LongestNoDataGapMs { get; init; }
    public bool PortErrorDuringCapture { get; init; }
    public bool TextLogSaved { get; init; }
    public bool RawFileSaved { get; init; }
    public bool SessionJsonSaved { get; init; }
    public long RawFileSize { get; init; }
    public GuidedSessionType? SessionType { get; init; }
    public bool IsStableSession { get; init; }
    public decimal? KnownWeightKg { get; init; }
    public bool HasKnownWeight { get; init; }
    public IReadOnlyList<CaptureEventMarker> Events { get; init; } = [];
}

public sealed class CaptureQualityResult
{
    public required CaptureQualityStatus Status { get; init; }
    public required IReadOnlyList<string> Messages { get; init; }
}

public sealed class StableSessionComparisonResult
{
    public int EmptyTotalBytes { get; init; }
    public int PersonTotalBytes { get; init; }
    public int EmptyUniqueByteCount { get; init; }
    public int PersonUniqueByteCount { get; init; }
    public IReadOnlyDictionary<byte, int> ByteFrequencyDifference { get; init; } = new Dictionary<byte, int>();
    public bool RawStreamsIdentical { get; init; }
    public double EstimatedDifferencePercentage { get; init; }
    public required string Summary { get; init; }
}

public sealed class GuidedSavedSession
{
    public required GuidedSessionType SessionType { get; init; }
    public required CaptureSaveResult SaveResult { get; init; }
    public required CaptureRecordingStats Stats { get; init; }
    public required CaptureQualityResult Quality { get; init; }
    public byte[] RawBytes { get; init; } = [];
}
