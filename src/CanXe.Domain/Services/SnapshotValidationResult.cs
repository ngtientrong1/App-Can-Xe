namespace CanXe.Domain.Services;

public enum SnapshotValidationSeverity
{
    Pass,
    Warning,
    Fail
}

public sealed class SnapshotValidationResult
{
    public SnapshotValidationSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;

    public bool IsPass => Severity is SnapshotValidationSeverity.Pass or SnapshotValidationSeverity.Warning;
    public bool IsHardFail => Severity == SnapshotValidationSeverity.Fail;

    public static SnapshotValidationResult Pass(string message = "OK") =>
        new() { Severity = SnapshotValidationSeverity.Pass, Message = message };

    public static SnapshotValidationResult Warning(string message) =>
        new() { Severity = SnapshotValidationSeverity.Warning, Message = message };

    public static SnapshotValidationResult Fail(string message) =>
        new() { Severity = SnapshotValidationSeverity.Fail, Message = message };
}
