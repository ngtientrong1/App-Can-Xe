namespace CanXe.Application.Models;

public enum DiagnosticStatus
{
    Pass,
    Fail,
    Warning,
    Skipped
}

public sealed class SystemDiagnosticCheck
{
    public required string Category { get; init; }
    public required string Name { get; init; }
    public DiagnosticStatus Status { get; init; }
    public string Detail { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
}

public sealed class SystemDiagnosticsResult
{
    public IReadOnlyList<SystemDiagnosticCheck> Checks { get; init; } = [];
    public int Passed => Checks.Count(c => c.Status == DiagnosticStatus.Pass);
    public int Failed => Checks.Count(c => c.Status == DiagnosticStatus.Fail);
    public int Warnings => Checks.Count(c => c.Status == DiagnosticStatus.Warning);
    public int Skipped => Checks.Count(c => c.Status == DiagnosticStatus.Skipped);
    public bool AllRequiredPassed => Failed == 0;
    public TimeSpan TotalDuration { get; init; }
}

public sealed class DiagnosticsRunOptions
{
    public bool IncludeApplication { get; init; } = true;
    public bool IncludeDatabase { get; init; } = true;
    public bool IncludeFfmpeg { get; init; } = true;
    public bool IncludeCameraConfig { get; init; } = true;
    public bool IncludeCameraConnection { get; init; } = true;
    public bool IncludeScale { get; init; } = true;
    public bool RequireCamera { get; init; }
    public bool RequireScale { get; init; }
    public bool SkipCamera { get; init; }
    public bool SkipScale { get; init; }
    public bool UseTestMedia { get; init; }
    public bool PublishCheck { get; init; }
    public bool CaptureCameraPipe { get; init; }
    public string? TestMediaPath { get; init; }
}
