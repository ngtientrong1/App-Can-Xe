using CanXe.ProtocolAnalyzer.Core.Models;

namespace CanXe.ProtocolAnalyzer.Core.Analysis;

public sealed class ProtocolCandidateScorer : IProtocolCandidateScorer
{
    public IReadOnlyList<DecodedCandidate> ScoreAndRank(
        IReadOnlyList<DecodedCandidate> candidates,
        IReadOnlyList<SerialLogSession> sessions)
    {
        var stableSessions = sessions.Where(s => s.WeightLabel.IsStableSession).ToList();
        var dynamicSessions = sessions.Where(s => !s.WeightLabel.IsStableSession).ToList();

        var scored = candidates.Select(c => ScoreCandidate(c, stableSessions, dynamicSessions)).ToList();
        return scored.OrderByDescending(c => c.Score).ThenByDescending(c => c.Confidence).ToList();
    }

    private static DecodedCandidate ScoreCandidate(
        DecodedCandidate candidate,
        IReadOnlyList<SerialLogSession> stableSessions,
        IReadOnlyList<SerialLogSession> dynamicSessions)
    {
        var reasons = candidate.Reasons.ToList();
        var warnings = candidate.Warnings.ToList();
        double score = 0;
        double confidence = 0;

        var matchedStable = 0;
        foreach (var session in stableSessions)
        {
            if (!candidate.DecodedValuesBySession.TryGetValue(session.FileName, out var decoded) || decoded is null)
                continue;

            matchedStable++;
            var known = (double?)session.WeightLabel.KnownWeightKg;
            if (known is null)
                continue;

            var diff = Math.Abs(decoded.Value - known.Value);
            var tolerance = session.WeightLabel.KnownWeightIsApproximate
                ? Math.Max(known.Value * 0.15, 5)
                : Math.Max(known.Value * 0.02, 1);

            if (diff <= tolerance)
            {
                score += 18;
                reasons.Add($"Gần known weight {session.FileName}: decoded={decoded:F1}, known={known:F1}.");
            }
            else if (diff <= tolerance * 3)
            {
                score += 8;
                reasons.Add($"Lệch vừa phải {session.FileName}: decoded={decoded:F1}, known={known:F1}.");
            }
        }

        if (matchedStable >= 2)
        {
            score += 12;
            confidence += 15;
            reasons.Add("Khớp trên nhiều phiên stable.");
        }
        else if (matchedStable == 1)
        {
            warnings.Add("Chỉ khớp một phiên stable — confidence bị giới hạn.");
            confidence = Math.Min(confidence, 39);
        }

        var stableValues = stableSessions
            .Select(s => candidate.DecodedValuesBySession.TryGetValue(s.FileName, out var v) ? v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToList();

        if (stableValues.Count >= 2 && IsMonotonic(stableValues))
        {
            score += 15;
            confidence += 10;
            reasons.Add("Thứ tự decoded tăng dần trên các phiên stable.");
        }

        foreach (var session in dynamicSessions)
        {
            if (!candidate.DecodedValuesBySession.TryGetValue(session.FileName, out var decoded) || decoded is null)
                continue;

            warnings.Add($"Phiên dynamic {session.FileName}: decoded={decoded:F1} — không dùng làm ground truth.");
            score += 3;
        }

        if (dynamicSessions.Count > 0 && stableSessions.Count > 0)
        {
            var stableSpread = ComputeSpread(stableSessions, candidate);
            var dynamicSpread = ComputeSpread(dynamicSessions, candidate);
            if (dynamicSpread > stableSpread * 1.2)
            {
                score += 8;
                reasons.Add("Phiên dynamic biến động lớn hơn phiên stable.");
            }
        }

        score += candidate.Transform.FrameLengthBytes is >= 1 and <= 32 ? 5 : 0;
        confidence = Math.Clamp(confidence + score * 0.35, 0, 84);

        return new DecodedCandidate
        {
            DecoderName = candidate.DecoderName,
            Transform = candidate.Transform,
            DecodedValues = candidate.DecodedValues,
            DecodedValuesBySession = candidate.DecodedValuesBySession,
            Score = Math.Clamp(score, 0, 100),
            Confidence = confidence,
            Reasons = reasons,
            Warnings = warnings
        };
    }

    private static bool IsMonotonic(IReadOnlyList<double> values)
    {
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] + 0.001 < values[i - 1])
                return false;
        }

        return true;
    }

    private static double ComputeSpread(IReadOnlyList<SerialLogSession> sessions, DecodedCandidate candidate)
    {
        var values = sessions
            .Select(s => candidate.DecodedValuesBySession.TryGetValue(s.FileName, out var v) ? v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (values.Count <= 1)
            return 0;

        return values.Max() - values.Min();
    }
}

public sealed class ProtocolAnalysisOrchestrator
{
    private readonly ISerialLogParser _parser;
    private readonly ISerialStreamReconstructor _reconstructor;
    private readonly ITimingAnalyzer _timingAnalyzer;
    private readonly IFrameCandidateDetector _frameDetector;
    private readonly IBitTransformAnalyzer _bitTransformAnalyzer;
    private readonly ICandidateDecoder _decoder;
    private readonly IProtocolCandidateScorer _scorer;

    public ProtocolAnalysisOrchestrator(
        ISerialLogParser? parser = null,
        ISerialStreamReconstructor? reconstructor = null,
        ITimingAnalyzer? timingAnalyzer = null,
        IFrameCandidateDetector? frameDetector = null,
        IBitTransformAnalyzer? bitTransformAnalyzer = null,
        ICandidateDecoder? decoder = null,
        IProtocolCandidateScorer? scorer = null)
    {
        _parser = parser ?? new Parsing.SerialLogParser();
        _reconstructor = reconstructor ?? new Reconstruction.SerialStreamReconstructor();
        _timingAnalyzer = timingAnalyzer ?? new TimingAnalyzer();
        _frameDetector = frameDetector ?? new FrameCandidateDetector();
        _bitTransformAnalyzer = bitTransformAnalyzer ?? new BitTransformAnalyzer();
        _decoder = decoder ?? new CandidateDecoderRegistry();
        _scorer = scorer ?? new ProtocolCandidateScorer();
    }

    public ProtocolAnalysisResult Analyze(IReadOnlyList<string> logFilePaths, ProtocolAnalysisOptions? options = null)
    {
        options ??= new ProtocolAnalysisOptions();
        var warnings = new List<ProtocolAnalysisWarning>();
        var sessions = new List<SerialLogSession>();

        foreach (var path in logFilePaths)
        {
            var session = _parser.ParseFile(path);
            warnings.AddRange(session.Warnings);
            session.Stream = _reconstructor.Reconstruct(session, warnings);
            session.Timing = _timingAnalyzer.Analyze(session);
            sessions.Add(session);
        }

        var combinedStream = CombineSessions(sessions);
        var frameCandidates = _frameDetector.Detect(combinedStream, options);
        var bitCandidates = _bitTransformAnalyzer.Analyze(combinedStream, options);
        var decoded = _decoder.Decode(sessions, bitCandidates, options);
        var ranked = _scorer.ScoreAndRank(decoded, sessions);

        return new ProtocolAnalysisResult
        {
            GeneratedAt = DateTimeOffset.Now,
            Sessions = sessions,
            TimingBySession = sessions.Select(s => s.Timing!).ToList(),
            FrameCandidates = frameCandidates,
            BitTransformCandidates = bitCandidates,
            DecoderCandidates = ranked,
            Warnings = warnings,
            TopCandidate = ranked.FirstOrDefault()
        };
    }

    private static ContinuousByteStream CombineSessions(IReadOnlyList<SerialLogSession> sessions)
    {
        var bytes = new List<byte>();
        var mappings = new List<StreamByteMapping>();
        var chunks = new List<SerialLogChunk>();

        foreach (var session in sessions)
        {
            if (session.Stream is null)
                continue;

            var baseOffset = bytes.Count;
            bytes.AddRange(session.Stream.Bytes);
            chunks.AddRange(session.Stream.SourceChunks);

            foreach (var mapping in session.Stream.Mappings)
            {
                mappings.Add(new StreamByteMapping
                {
                    StreamOffset = baseOffset + mapping.StreamOffset,
                    ChunkNumber = mapping.ChunkNumber,
                    ChunkByteIndex = mapping.ChunkByteIndex
                });
            }
        }

        return new ContinuousByteStream
        {
            Bytes = bytes.ToArray(),
            Mappings = mappings,
            SourceChunks = chunks
        };
    }
}
