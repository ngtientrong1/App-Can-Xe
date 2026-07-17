using System.IO;
using System.Runtime.CompilerServices;
using CanXe.Infrastructure.Logging;

namespace CanXe.Tests.Support;

/// <summary>
/// Isolates CanXe.Tests testhost log output per process.
/// </summary>
internal static class AppTestLogBootstrap
{
    internal static string? IsolatedLogDirectory { get; private set; }

    [ModuleInitializer]
    internal static void Init()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "CanXeTestLogs",
            "app-testhost-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        IsolatedLogDirectory = dir;
        CanXeLogPaths.SetOverrideRoot(dir);
        Environment.SetEnvironmentVariable("CANXE_LOG_DIR", dir);
    }
}
