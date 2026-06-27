using System.Globalization;
using System.Text.RegularExpressions;
using CanXe.ProtocolAnalyzer.Core.Models;

namespace CanXe.ProtocolAnalyzer.Core.Parsing;

public sealed class SerialLogParser : ISerialLogParser
{
    private static readonly Regex HexToken = new(@"^[0-9A-Fa-f]{1,2}$", RegexOptions.CultureInvariant);

    public SerialLogSession ParseFile(string filePath, KnownWeightLabel? defaultLabel = null)
    {
        var warnings = new List<ProtocolAnalysisWarning>();
        var metadata = new SerialLogMetadata();
        var chunks = new List<SerialLogChunk>();
        var lines = File.ReadAllLines(filePath);
        var fileName = Path.GetFileName(filePath);

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (line.Equals("Session started:", StringComparison.OrdinalIgnoreCase))
            {
                i = ParseMetadataBlock(lines, i + 1, metadata);
                continue;
            }

            if (line.StartsWith("--- Session notes updated ---", StringComparison.OrdinalIgnoreCase))
            {
                i = ParseMetadataBlock(lines, i + 1, metadata);
                continue;
            }

            if (line.StartsWith("Timestamp:", StringComparison.OrdinalIgnoreCase))
            {
                var chunkResult = TryParseChunk(lines, ref i, filePath, warnings);
                if (chunkResult is not null)
                    chunks.Add(chunkResult);
                continue;
            }

            i++;
        }

        var label = defaultLabel ?? SessionImportDefaults.FromFileName(fileName);
        return new SerialLogSession
        {
            SourceFilePath = filePath,
            FileName = fileName,
            Metadata = metadata,
            WeightLabel = label,
            Chunks = chunks,
            Warnings = warnings
        };
    }

    private static int ParseMetadataBlock(string[] lines, int startIndex, SerialLogMetadata metadata)
    {
        var i = startIndex;
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                break;

            if (line.StartsWith("Timestamp:", StringComparison.OrdinalIgnoreCase))
                break;

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                ApplyMetadata(metadata, key, value);
            }

            i++;
        }

        return i;
    }

    private static void ApplyMetadata(SerialLogMetadata metadata, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "port":
                metadata.PortName = value;
                break;
            case "baudrate":
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var baud))
                    metadata.BaudRate = baud;
                break;
            case "databits":
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dataBits))
                    metadata.DataBits = dataBits;
                break;
            case "parity":
                metadata.Parity = value;
                break;
            case "stopbits":
                metadata.StopBits = value;
                break;
            case "handshake":
                metadata.Handshake = value;
                break;
            case "encoding":
                metadata.Encoding = value;
                break;
            case "windows version":
                metadata.WindowsVersion = value;
                break;
            case "application version":
                metadata.ApplicationVersion = value;
                break;
            case "session label":
                metadata.SessionLabel = value;
                break;
            case "scale display weight":
                metadata.ScaleDisplayWeight = value;
                break;
            case "vehicle condition":
                metadata.VehicleCondition = value;
                break;
            case "session start note":
                metadata.SessionStartNote = value;
                break;
            case "additional notes":
                metadata.AdditionalNotes = value;
                break;
        }
    }

    private static SerialLogChunk? TryParseChunk(
        string[] lines,
        ref int index,
        string filePath,
        IList<ProtocolAnalysisWarning> warnings)
    {
        var timestampLine = lines[index].Trim();
        index++;

        if (!TryParseTimestamp(timestampLine["Timestamp:".Length..].Trim(), out var timestamp))
        {
            warnings.Add(Warning("PARSE_TIMESTAMP", "Không parse được timestamp.", filePath));
            return null;
        }

        int chunkNumber = -1;
        int declaredCount = 0;
        string? hexLine = null;
        var truncated = false;

        while (index < lines.Length)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrEmpty(line))
            {
                index++;
                break;
            }

            if (line.StartsWith("Timestamp:", StringComparison.OrdinalIgnoreCase))
                break;

            if (line.StartsWith("Chunk number:", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line["Chunk number:".Length..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out chunkNumber);
            }
            else if (line.StartsWith("Byte count:", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line["Byte count:".Length..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out declaredCount);
            }
            else if (line.StartsWith("HEX:", StringComparison.OrdinalIgnoreCase))
            {
                hexLine = line["HEX:".Length..].Trim();
            }
            else if (line.StartsWith("Escaped text:", StringComparison.OrdinalIgnoreCase))
            {
                // Ignored by design — HEX is authoritative.
            }

            index++;
        }

        if (chunkNumber < 0)
        {
            warnings.Add(Warning("PARSE_CHUNK", "Chunk thiếu chunk number.", filePath));
            return null;
        }

        var (bytes, hexTruncated) = ParseHex(hexLine);
        truncated = hexTruncated;

        if (declaredCount > 0 && bytes.Length != declaredCount)
        {
            warnings.Add(new ProtocolAnalysisWarning
            {
                Severity = ProtocolWarningSeverity.Warning,
                Code = "BYTE_COUNT_MISMATCH",
                Message = $"Chunk {chunkNumber}: Byte count={declaredCount} nhưng HEX có {bytes.Length} byte.",
                SourceFile = filePath,
                ChunkNumber = chunkNumber
            });
        }

        if (truncated)
        {
            warnings.Add(new ProtocolAnalysisWarning
            {
                Severity = ProtocolWarningSeverity.Warning,
                Code = "TRUNCATED_HEX",
                Message = $"Chunk {chunkNumber}: dòng HEX bị cắt, giữ {bytes.Length} byte hợp lệ.",
                SourceFile = filePath,
                ChunkNumber = chunkNumber
            });
        }

        return new SerialLogChunk
        {
            Timestamp = timestamp,
            ChunkNumber = chunkNumber,
            DeclaredByteCount = declaredCount,
            Bytes = bytes,
            IsTruncated = truncated
        };
    }

    internal static (byte[] Bytes, bool Truncated) ParseHex(string? hexLine)
    {
        if (string.IsNullOrWhiteSpace(hexLine))
            return ([], false);

        var tokens = hexLine.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var bytes = new List<byte>(tokens.Length);
        var truncated = false;

        foreach (var token in tokens)
        {
            if (!HexToken.IsMatch(token))
            {
                truncated = true;
                break;
            }

            bytes.Add(byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        return (bytes.ToArray(), truncated);
    }

    private static bool TryParseTimestamp(string value, out DateTimeOffset timestamp)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out timestamp))
            return true;

        return DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out timestamp);
    }

    private static ProtocolAnalysisWarning Warning(string code, string message, string file) => new()
    {
        Severity = ProtocolWarningSeverity.Warning,
        Code = code,
        Message = message,
        SourceFile = file
    };
}

public static class SessionImportDefaults
{
    public static KnownWeightLabel FromFileName(string fileName)
    {
        if (fileName.Contains("145439", StringComparison.Ordinal))
            return new KnownWeightLabel { KnownWeightKg = 0, KnownWeightIsApproximate = true, IsStableSession = true };
        if (fileName.Contains("145617", StringComparison.Ordinal))
            return new KnownWeightLabel { KnownWeightKg = 50, KnownWeightIsApproximate = true, IsStableSession = true };
        if (fileName.Contains("145814", StringComparison.Ordinal))
            return new KnownWeightLabel { KnownWeightKg = 5030, KnownWeightIsApproximate = false, IsStableSession = true };
        if (fileName.Contains("145841", StringComparison.Ordinal))
            return new KnownWeightLabel { KnownWeightKg = 5080, KnownWeightIsApproximate = true, IsStableSession = false };

        return new KnownWeightLabel();
    }
}
