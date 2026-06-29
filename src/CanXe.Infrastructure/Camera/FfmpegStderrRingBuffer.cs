namespace CanXe.Infrastructure.Camera;

internal sealed class FfmpegStderrRingBuffer
{
    public const int DefaultCapacity = 50;

    private readonly List<string> _lines = new(DefaultCapacity);

    public void Add(string line)
    {
        if (_lines.Count >= DefaultCapacity)
            _lines.RemoveAt(0);

        _lines.Add(line);
    }

    public IReadOnlyList<string> GetTail(int maxLines)
    {
        if (_lines.Count == 0)
            return Array.Empty<string>();

        var skip = Math.Max(0, _lines.Count - maxLines);
        return _lines.Skip(skip).ToArray();
    }
}

public static class FfmpegDecoderDiagnostics
{
    public const int StderrTailLineCount = 50;

    public static string BuildEarlyExitError(IReadOnlyList<string> stderrLines, int? exitCode)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("Process exited before first decoded frame.");
        if (exitCode.HasValue)
            builder.Append($" ExitCode={exitCode.Value}.");

        if (stderrLines.Count == 0)
            return builder.ToString();

        builder.AppendLine();
        builder.AppendLine("Stderr tail:");
        foreach (var line in stderrLines)
            builder.AppendLine(RtspCredentialRedactor.Redact(line));

        return builder.ToString().TrimEnd();
    }

    public static bool IsFatalOptionError(string line) =>
        line.Contains("Unrecognized option", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Error splitting the argument list", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Option not found", StringComparison.OrdinalIgnoreCase);
}
