using CanXe.ProtocolAnalyzer.Core.Models;

namespace CanXe.ProtocolAnalyzer.Core;

public interface ISerialLogParser
{
    SerialLogSession ParseFile(string filePath, KnownWeightLabel? defaultLabel = null);
}

public interface ISerialStreamReconstructor
{
    ContinuousByteStream Reconstruct(SerialLogSession session, IList<ProtocolAnalysisWarning>? warnings = null);
}

public interface ITimingAnalyzer
{
    TimingStatistics Analyze(SerialLogSession session);
}

public interface IFrameCandidateDetector
{
    IReadOnlyList<FrameCandidate> Detect(ContinuousByteStream stream, ProtocolAnalysisOptions? options = null);
}

public interface IBitTransformAnalyzer
{
    IReadOnlyList<BitTransformCandidate> Analyze(ContinuousByteStream stream, ProtocolAnalysisOptions? options = null);
}

public interface ICandidateDecoder
{
    IReadOnlyList<DecodedCandidate> Decode(
        IReadOnlyList<SerialLogSession> sessions,
        IReadOnlyList<BitTransformCandidate> transforms,
        ProtocolAnalysisOptions? options = null);
}

public interface IProtocolCandidateScorer
{
    IReadOnlyList<DecodedCandidate> ScoreAndRank(
        IReadOnlyList<DecodedCandidate> candidates,
        IReadOnlyList<SerialLogSession> sessions);
}

public interface IProtocolAnalysisReportWriter
{
    Task WriteMarkdownAsync(ProtocolAnalysisResult result, string filePath, CancellationToken cancellationToken = default);
    Task WriteJsonAsync(ProtocolAnalysisResult result, string filePath, CancellationToken cancellationToken = default);
}
