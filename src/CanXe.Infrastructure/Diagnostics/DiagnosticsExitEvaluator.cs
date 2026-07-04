using CanXe.Application.Models;

namespace CanXe.Infrastructure.Diagnostics;

public sealed record DiagnosticsExitSummary(
    bool RequiredScale,
    string ScaleResult,
    int ExitCode);

public static class DiagnosticsExitEvaluator
{
    public static DiagnosticsExitSummary Evaluate(SystemDiagnosticsResult result, DiagnosticsRunOptions options)
    {
        var scaleResult = EvaluateCategory(result, "Scale", options.RequireScale, options.SkipScale);

        var requiredFailed =
            (options.RequireScale && scaleResult != "PASS")
            || result.Failed > 0
            || (options.RequireScale && result.Checks.Any(c =>
                c.Category == "Scale" && c.Status is DiagnosticStatus.Warning));

        if (requiredFailed)
            return new DiagnosticsExitSummary(options.RequireScale, scaleResult, 1);

        return new DiagnosticsExitSummary(options.RequireScale, scaleResult, 0);
    }

    private static string EvaluateCategory(
        SystemDiagnosticsResult result,
        string category,
        bool required,
        bool skipped)
    {
        if (skipped)
            return "SKIPPED";

        var checks = result.Checks.Where(c => c.Category == category).ToList();
        if (checks.Count == 0)
            return required ? "FAIL" : "SKIP";

        if (checks.Any(c => c.Status == DiagnosticStatus.Fail))
            return "FAIL";

        if (required && checks.Any(c => c.Status is DiagnosticStatus.Skipped or DiagnosticStatus.Warning))
            return "FAIL";

        if (!required)
        {
            if (checks.Any(c => c.Status == DiagnosticStatus.Pass))
                return "PASS";
            return checks.All(c => c.Status == DiagnosticStatus.Skipped) ? "SKIP" : "PASS";
        }

        return checks.All(c => c.Status == DiagnosticStatus.Pass) ? "PASS" : "FAIL";
    }
}
