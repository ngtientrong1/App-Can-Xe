namespace CanXe.Infrastructure.Logging;

/// <summary>
/// Central log directory resolution for CanXe diagnostic files.
/// Stress-test root cause: parallel testhost processes contended on
/// %LocalAppData%\CanXe\Logs via exclusive File.AppendAllText (IOException).
/// Override via CANXE_LOG_DIR env or SetOverrideRoot isolates each process.
/// </summary>
public static class CanXeLogPaths
{
    private static string? _overrideRoot;

    public static void SetOverrideRoot(string? root) => _overrideRoot = root;

    public static void ClearOverrideRoot() => _overrideRoot = null;

    public static string LogsDirectory
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("CANXE_LOG_DIR");
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            if (!string.IsNullOrWhiteSpace(_overrideRoot))
                return _overrideRoot;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CanXe",
                "Logs");
        }
    }

    public static string GetLogFile(string fileName) => Path.Combine(LogsDirectory, fileName);
}
