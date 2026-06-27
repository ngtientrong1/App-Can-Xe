using CanXe.ProtocolAnalyzer.Core.Models;

namespace CanXe.ProtocolAnalyzer.Core.Analysis;

public sealed class TimingAnalyzer : ITimingAnalyzer
{
    public TimingStatistics Analyze(SerialLogSession session)
    {
        var chunks = session.Chunks.OrderBy(c => c.Timestamp).ThenBy(c => c.ChunkNumber).ToList();
        if (chunks.Count == 0)
        {
            return new TimingStatistics
            {
                TotalDuration = TimeSpan.Zero,
                TotalBytes = 0,
                TotalCallbacks = 0,
                BytesPerSecond = 0,
                CallbacksPerSecond = 0
            };
        }

        var totalBytes = chunks.Sum(c => c.Bytes.Length);
        var duration = chunks[^1].Timestamp - chunks[0].Timestamp;
        var durationSec = Math.Max(duration.TotalSeconds, 0.001);

        var gaps = new List<double>();
        for (var i = 1; i < chunks.Count; i++)
            gaps.Add((chunks[i].Timestamp - chunks[i - 1].Timestamp).TotalMilliseconds);

        var distribution = chunks
            .GroupBy(c => c.Bytes.Length)
            .ToDictionary(g => g.Key, g => g.Count());

        var mean = gaps.Count > 0 ? gaps.Average() : 0;
        var median = ComputeMedian(gaps);
        var stdDev = ComputeStdDev(gaps, mean);
        var abnormal = gaps.Where(g => mean > 0 && g > mean * 3).ToList();
        var periodicity = DetectPeriodicityCandidates(gaps);

        return new TimingStatistics
        {
            TotalDuration = duration,
            TotalBytes = totalBytes,
            TotalCallbacks = chunks.Count,
            BytesPerSecond = totalBytes / durationSec,
            CallbacksPerSecond = chunks.Count / durationSec,
            ChunkSizeDistribution = distribution,
            InterCallbackGapMs = gaps,
            MinGapMs = gaps.Count > 0 ? gaps.Min() : 0,
            MaxGapMs = gaps.Count > 0 ? gaps.Max() : 0,
            MeanGapMs = mean,
            MedianGapMs = median,
            StdDevGapMs = stdDev,
            AbnormalGapMs = abnormal,
            PeriodicityCandidatesMs = periodicity
        };
    }

    internal static double ComputeMedian(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    private static double ComputeStdDev(IReadOnlyList<double> values, double mean)
    {
        if (values.Count <= 1)
            return 0;

        var sum = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sum / values.Count);
    }

    private static IReadOnlyList<int> DetectPeriodicityCandidates(IReadOnlyList<double> gaps)
    {
        if (gaps.Count < 4)
            return [];

        var candidates = new Dictionary<int, int>();
        var rounded = gaps.Select(g => (int)Math.Round(g)).Where(g => g > 0).ToList();
        for (var period = 1; period <= 64; period++)
        {
            var hits = 0;
            for (var i = period; i < rounded.Count; i++)
            {
                if (Math.Abs(rounded[i] - rounded[i - period]) <= 2)
                    hits++;
            }

            if (hits >= 3)
                candidates[period] = hits;
        }

        return candidates.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Key).ToList();
    }
}

public sealed class FrameCandidateDetector : IFrameCandidateDetector
{
    public IReadOnlyList<FrameCandidate> Detect(ContinuousByteStream stream, ProtocolAnalysisOptions? options = null)
    {
        options ??= new ProtocolAnalysisOptions();
        var bytes = stream.Bytes;
        if (bytes.Length < options.MinFrameLengthBytes)
            return [];

        var results = new List<FrameCandidate>();
        for (var frameLen = options.MinFrameLengthBytes; frameLen <= options.MaxFrameLengthBytes; frameLen++)
        {
            if (bytes.Length < frameLen * 2)
                continue;

            var autocorr = AutocorrelationScore(bytes, frameLen);
            var repeated = RepeatedSubsequenceScore(bytes, frameLen);
            var score = (autocorr + repeated) / 2.0;
            if (score < 0.15)
                continue;

            var sample = bytes.Take(frameLen).ToArray();
            results.Add(new FrameCandidate
            {
                FrameLengthBytes = frameLen,
                OccurrenceCount = bytes.Length / frameLen,
                StabilityScore = score,
                DetectionMethod = "autocorrelation+repeated-subsequence",
                SampleFrame = sample
            });
        }

        return results.OrderByDescending(r => r.StabilityScore).Take(20).ToList();
    }

    internal static double AutocorrelationScore(byte[] bytes, int period)
    {
        if (bytes.Length <= period)
            return 0;

        var matches = 0;
        var total = 0;
        for (var i = period; i < bytes.Length; i++)
        {
            total++;
            if (bytes[i] == bytes[i - period])
                matches++;
        }

        return total == 0 ? 0 : (double)matches / total;
    }

    internal static double RepeatedSubsequenceScore(byte[] bytes, int period)
    {
        if (bytes.Length < period * 2)
            return 0;

        var first = bytes.AsSpan(0, period);
        var matches = 0;
        var windows = 0;
        for (var offset = period; offset + period <= bytes.Length; offset += period)
        {
            windows++;
            if (first.SequenceEqual(bytes.AsSpan(offset, period)))
                matches++;
        }

        return windows == 0 ? 0 : (double)matches / windows;
    }
}
