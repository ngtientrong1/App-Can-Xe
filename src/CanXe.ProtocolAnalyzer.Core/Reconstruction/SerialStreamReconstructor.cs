using CanXe.ProtocolAnalyzer.Core.Models;

namespace CanXe.ProtocolAnalyzer.Core.Reconstruction;

public sealed class SerialStreamReconstructor : ISerialStreamReconstructor
{
    public ContinuousByteStream Reconstruct(SerialLogSession session, IList<ProtocolAnalysisWarning>? warnings = null)
    {
        warnings ??= [];

        var ordered = session.Chunks
            .OrderBy(c => c.Timestamp)
            .ThenBy(c => c.ChunkNumber)
            .ToList();

        DetectChunkOrderingIssues(ordered, session.SourceFilePath, warnings);

        var bytes = new List<byte>();
        var mappings = new List<StreamByteMapping>();

        foreach (var chunk in ordered)
        {
            for (var i = 0; i < chunk.Bytes.Length; i++)
            {
                mappings.Add(new StreamByteMapping
                {
                    StreamOffset = bytes.Count,
                    ChunkNumber = chunk.ChunkNumber,
                    ChunkByteIndex = i
                });
                bytes.Add(chunk.Bytes[i]);
            }
        }

        return new ContinuousByteStream
        {
            Bytes = bytes.ToArray(),
            Mappings = mappings,
            SourceChunks = ordered
        };
    }

    private static void DetectChunkOrderingIssues(
        IReadOnlyList<SerialLogChunk> ordered,
        string filePath,
        IList<ProtocolAnalysisWarning> warnings)
    {
        if (ordered.Count == 0)
            return;

        var seen = new HashSet<int>();
        var duplicates = new HashSet<int>();
        foreach (var chunk in ordered)
        {
            if (!seen.Add(chunk.ChunkNumber))
                duplicates.Add(chunk.ChunkNumber);
        }

        foreach (var dup in duplicates)
        {
            warnings.Add(new ProtocolAnalysisWarning
            {
                Severity = ProtocolWarningSeverity.Warning,
                Code = "DUPLICATE_CHUNK",
                Message = $"Chunk number {dup} xuất hiện nhiều lần.",
                SourceFile = filePath,
                ChunkNumber = dup
            });
        }

        var numbers = ordered.Select(c => c.ChunkNumber).Distinct().OrderBy(n => n).ToList();
        for (var i = 1; i < numbers.Count; i++)
        {
            if (numbers[i] - numbers[i - 1] > 1)
            {
                warnings.Add(new ProtocolAnalysisWarning
                {
                    Severity = ProtocolWarningSeverity.Info,
                    Code = "MISSING_CHUNK",
                    Message = $"Thiếu chunk giữa {numbers[i - 1]} và {numbers[i]}.",
                    SourceFile = filePath
                });
            }
        }

        var outOfOrder = sessionChunksOutOfChunkNumberOrder(ordered);
        if (outOfOrder)
        {
            warnings.Add(new ProtocolAnalysisWarning
            {
                Severity = ProtocolWarningSeverity.Info,
                Code = "CHUNK_REORDERED",
                Message = "Chunk được sắp xếp lại theo timestamp rồi chunk number.",
                SourceFile = filePath
            });
        }
    }

    private static bool sessionChunksOutOfChunkNumberOrder(IReadOnlyList<SerialLogChunk> ordered)
    {
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].ChunkNumber < ordered[i - 1].ChunkNumber)
                return true;
        }

        return false;
    }
}
