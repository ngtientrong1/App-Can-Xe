using System.IO;
using CanXe.Infrastructure.Logging;

namespace CanXe.Tests.Application;

public sealed class SafeLogFileAppendTests
{
    [Fact]
    public void ConcurrentAppend_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "CanXeSafeLogTests", Guid.NewGuid().ToString("N") + ".log");
        try
        {
            Parallel.For(0, 32, i =>
            {
                SafeLogFileAppend.AppendLine(path, $"line-{i}");
            });

            var lines = File.ReadAllLines(path);
            Assert.Equal(32, lines.Length);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void OverrideRoot_RedirectsAwayFromLocalAppData()
    {
        var previousEnv = Environment.GetEnvironmentVariable("CANXE_LOG_DIR");
        var previousOverride = CanXe.Tests.Support.AppTestLogBootstrap.IsolatedLogDirectory;
        var isolated = Path.Combine(Path.GetTempPath(), "CanXeSafeLogTests", "override-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CANXE_LOG_DIR", isolated);
            CanXeLogPaths.ClearOverrideRoot();

            var logFile = CanXeLogPaths.GetLogFile("safe-log-test.log");
            SafeLogFileAppend.AppendLine(logFile, "redirect-check");

            var localAppDataLogs = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CanXe",
                "Logs",
                "safe-log-test.log");

            Assert.StartsWith(isolated, logFile, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(logFile));
            Assert.False(File.Exists(localAppDataLogs));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CANXE_LOG_DIR", previousEnv);
            if (!string.IsNullOrWhiteSpace(previousOverride))
                CanXeLogPaths.SetOverrideRoot(previousOverride);
            try { if (Directory.Exists(isolated)) Directory.Delete(isolated, true); } catch { }
        }
    }
}
