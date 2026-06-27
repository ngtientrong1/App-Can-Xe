using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanXe.ProtocolAnalyzer.Core.Models;

namespace CanXe.ProtocolAnalyzer.Core.Reporting;

public sealed class ProtocolAnalysisReportWriter : IProtocolAnalysisReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task WriteMarkdownAsync(ProtocolAnalysisResult result, string filePath, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Protocol Analysis Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {result.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine("## Sessions");
        sb.AppendLine();
        sb.AppendLine("| File | Known kg | Stable | Chunks | Bytes | Duration |");
        sb.AppendLine("|------|----------|--------|--------|-------|----------|");
        foreach (var s in result.Sessions)
        {
            var timing = s.Timing;
            sb.AppendLine($"| {s.FileName} | {s.WeightLabel.KnownWeightKg} | {s.WeightLabel.IsStableSession} | {s.Chunks.Count} | {timing?.TotalBytes ?? 0} | {timing?.TotalDuration} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Timing summary");
        foreach (var (session, timing) in result.Sessions.Zip(result.TimingBySession))
        {
            sb.AppendLine();
            sb.AppendLine($"### {session.FileName}");
            sb.AppendLine($"- Bytes/s: {timing.BytesPerSecond:F2}");
            sb.AppendLine($"- Callbacks/s: {timing.CallbacksPerSecond:F2}");
            sb.AppendLine($"- Median gap ms: {timing.MedianGapMs:F2}");
            sb.AppendLine($"- Periodicity candidates ms: {string.Join(", ", timing.PeriodicityCandidatesMs)}");
        }

        sb.AppendLine();
        sb.AppendLine("## Frame length candidates");
        foreach (var frame in result.FrameCandidates.Take(10))
        {
            sb.AppendLine($"- {frame.FrameLengthBytes} bytes — score {frame.StabilityScore:F3} ({frame.DetectionMethod})");
        }

        sb.AppendLine();
        sb.AppendLine("## Top decoder candidates");
        sb.AppendLine();
        sb.AppendLine("| Candidate | Score | Confidence | Transform |");
        sb.AppendLine("|-----------|-------|------------|-----------|");
        foreach (var c in result.DecoderCandidates.Take(15))
        {
            sb.AppendLine($"| {c.DecoderName} | {c.Score:F1} | {c.Confidence:F1} | {c.Transform.Name} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Comparison table");
        sb.AppendLine();
        sb.AppendLine("| Candidate | 0 kg | 50 kg | 5030 kg | Dynamic | Consistency | Score | Confidence |");
        sb.AppendLine("|-----------|------|-------|---------|---------|-------------|-------|------------|");
        foreach (var c in result.DecoderCandidates.Take(15))
        {
            sb.AppendLine($"| {c.DecoderName} | {Fmt(c, "145439")} | {Fmt(c, "145617")} | {Fmt(c, "145814")} | {Fmt(c, "145841")} | {c.Reasons.Count} reasons | {c.Score:F1} | {c.Confidence:F1} |");
        }

        if (result.TopCandidate is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Top candidate detail");
            sb.AppendLine($"- Decoder: {result.TopCandidate.DecoderName}");
            sb.AppendLine($"- Score: {result.TopCandidate.Score:F1}");
            sb.AppendLine($"- Confidence: {result.TopCandidate.Confidence:F1} (not production-ready)");
            foreach (var reason in result.TopCandidate.Reasons)
                sb.AppendLine($"- Reason: {reason}");
            foreach (var warning in result.TopCandidate.Warnings)
                sb.AppendLine($"- Warning: {warning}");
        }

        sb.AppendLine();
        sb.AppendLine("## Warnings");
        foreach (var w in result.Warnings.Take(50))
            sb.AppendLine($"- [{w.Code}] {w.Message}");

        sb.AppendLine();
        sb.AppendLine("> Phase 2B: No candidate is production-ready.");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, cancellationToken);
    }

    public async Task WriteJsonAsync(ProtocolAnalysisResult result, string filePath, CancellationToken cancellationToken = default)
    {
        var dto = new
        {
            result.GeneratedAt,
            Sessions = result.Sessions.Select(s => new
            {
                s.FileName,
                s.WeightLabel.KnownWeightKg,
                s.WeightLabel.KnownWeightIsApproximate,
                s.WeightLabel.IsStableSession,
                ChunkCount = s.Chunks.Count,
                TotalBytes = s.Timing?.TotalBytes ?? 0,
                DurationMs = s.Timing?.TotalDuration.TotalMilliseconds ?? 0
            }),
            FrameCandidates = result.FrameCandidates,
            TopCandidates = result.DecoderCandidates.Take(20),
            Warnings = result.Warnings
        };

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(dto, JsonOptions), Encoding.UTF8, cancellationToken);
    }

    private static string Fmt(DecodedCandidate c, string fileKey)
    {
        var match = c.DecodedValuesBySession.FirstOrDefault(kv => kv.Key.Contains(fileKey, StringComparison.Ordinal));
        return match.Value?.ToString("F1") ?? "—";
    }
}
