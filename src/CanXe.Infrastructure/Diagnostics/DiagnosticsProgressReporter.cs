namespace CanXe.Infrastructure.Diagnostics;

public static class DiagnosticsProgressReporter
{
    public static void RunStarted(string phase) =>
        Console.WriteLine($"[RUN ] {phase}");

    public static void RunPassed(string phase) =>
        Console.WriteLine($"[PASS] {phase} completed");

    public static void RunFailed(string phase, string detail) =>
        Console.Error.WriteLine($"[FAIL] {phase}: {detail}");
}
