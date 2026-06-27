namespace CanXe.ProtocolAnalyzer.Core.Models;

public sealed class SerialLogMetadata
{
    public string? PortName { get; set; }
    public int? BaudRate { get; set; }
    public int? DataBits { get; set; }
    public string? Parity { get; set; }
    public string? StopBits { get; set; }
    public string? Handshake { get; set; }
    public string? Encoding { get; set; }
    public string? WindowsVersion { get; set; }
    public string? ApplicationVersion { get; set; }
    public string? SessionLabel { get; set; }
    public string? ScaleDisplayWeight { get; set; }
    public string? VehicleCondition { get; set; }
    public string? SessionStartNote { get; set; }
    public string? AdditionalNotes { get; set; }
}

public sealed class SerialLogChunk
{
    public required DateTimeOffset Timestamp { get; init; }
    public required int ChunkNumber { get; init; }
    public required int DeclaredByteCount { get; init; }
    public required byte[] Bytes { get; init; }
    public bool IsTruncated { get; init; }
}

public sealed class KnownWeightLabel
{
    public decimal? KnownWeightKg { get; set; }
    public bool KnownWeightIsApproximate { get; set; }
    public bool IsStableSession { get; set; } = true;
}

public sealed class SerialLogSession
{
    public required string SourceFilePath { get; init; }
    public required string FileName { get; init; }
    public SerialLogMetadata Metadata { get; init; } = new();
    public KnownWeightLabel WeightLabel { get; set; } = new();
    public IReadOnlyList<SerialLogChunk> Chunks { get; init; } = [];
    public ContinuousByteStream? Stream { get; set; }
    public TimingStatistics? Timing { get; set; }
    public IReadOnlyList<ProtocolAnalysisWarning> Warnings { get; init; } = [];
}

public sealed class StreamByteMapping
{
    public required int StreamOffset { get; init; }
    public required int ChunkNumber { get; init; }
    public required int ChunkByteIndex { get; init; }
}

public sealed class ContinuousByteStream
{
    public required byte[] Bytes { get; init; }
    public required IReadOnlyList<StreamByteMapping> Mappings { get; init; }
    public required IReadOnlyList<SerialLogChunk> SourceChunks { get; init; }
}

public sealed class TimingStatistics
{
    public TimeSpan TotalDuration { get; init; }
    public int TotalBytes { get; init; }
    public int TotalCallbacks { get; init; }
    public double BytesPerSecond { get; init; }
    public double CallbacksPerSecond { get; init; }
    public IReadOnlyDictionary<int, int> ChunkSizeDistribution { get; init; } = new Dictionary<int, int>();
    public IReadOnlyList<double> InterCallbackGapMs { get; init; } = [];
    public double MinGapMs { get; init; }
    public double MaxGapMs { get; init; }
    public double MeanGapMs { get; init; }
    public double MedianGapMs { get; init; }
    public double StdDevGapMs { get; init; }
    public IReadOnlyList<double> AbnormalGapMs { get; init; } = [];
    public IReadOnlyList<int> PeriodicityCandidatesMs { get; init; } = [];
}

public sealed class FrameCandidate
{
    public required int FrameLengthBytes { get; init; }
    public required int OccurrenceCount { get; init; }
    public required double StabilityScore { get; init; }
    public required string DetectionMethod { get; init; }
    public byte[]? SampleFrame { get; init; }
}

public sealed record BitTransformSpec
{
    public required string Name { get; init; }
    public required bool ZeroIsBit0 { get; init; }
    public required bool MsbFirst { get; init; }
    public required int BitOffset { get; init; }
    public required bool InvertBits { get; init; }
    public required bool ReverseBytes { get; init; }
    public required bool ReverseFrame { get; init; }
    public required int FrameLengthBytes { get; init; }
}

public sealed class BitTransformCandidate
{
    public required BitTransformSpec Spec { get; init; }
    public required byte[] TransformedBytes { get; init; }
    public required double PatternScore { get; init; }
}

public sealed class DecodedCandidate
{
    public required string DecoderName { get; init; }
    public required BitTransformSpec Transform { get; init; }
    public required IReadOnlyList<double?> DecodedValues { get; init; }
    public required double Score { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public IReadOnlyDictionary<string, double?> DecodedValuesBySession { get; init; } = new Dictionary<string, double?>();
}

public enum ProtocolWarningSeverity
{
    Info,
    Warning,
    Error
}

public sealed class ProtocolAnalysisWarning
{
    public required ProtocolWarningSeverity Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? SourceFile { get; init; }
    public int? ChunkNumber { get; init; }
}

public sealed class ProtocolAnalysisResult
{
    public required DateTimeOffset GeneratedAt { get; init; }
    public required IReadOnlyList<SerialLogSession> Sessions { get; init; }
    public required IReadOnlyList<TimingStatistics> TimingBySession { get; init; }
    public required IReadOnlyList<FrameCandidate> FrameCandidates { get; init; }
    public required IReadOnlyList<BitTransformCandidate> BitTransformCandidates { get; init; }
    public required IReadOnlyList<DecodedCandidate> DecoderCandidates { get; init; }
    public required IReadOnlyList<ProtocolAnalysisWarning> Warnings { get; init; }
    public DecodedCandidate? TopCandidate { get; init; }
}

public sealed class ProtocolAnalysisOptions
{
    public int MinFrameLengthBytes { get; set; } = 1;
    public int MaxFrameLengthBytes { get; set; } = 64;
    public int MaxBitTransformCandidates { get; set; } = 48;
    public int MaxDecoderCandidates { get; set; } = 120;
    public double AbnormalGapMultiplier { get; set; } = 3.0;
}
