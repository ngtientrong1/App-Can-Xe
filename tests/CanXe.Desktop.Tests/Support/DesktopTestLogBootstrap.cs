using System.IO;
using System.Runtime.CompilerServices;
using CanXe.Infrastructure.Logging;

namespace CanXe.Desktop.Tests.Support;

/// <summary>
/// Isolates Desktop testhost log output away from %LocalAppData%\CanXe\Logs
/// so parallel dotnet test assemblies do not contend on scale-mode.log.
/// </summary>
internal static class DesktopTestLogBootstrap
{
    internal static string? IsolatedLogDirectory { get; private set; }

    [ModuleInitializer]
    internal static void Init()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "CanXeTestLogs",
            "desktop-testhost-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        IsolatedLogDirectory = dir;
        CanXeLogPaths.SetOverrideRoot(dir);
        Environment.SetEnvironmentVariable("CANXE_LOG_DIR", dir);
    }
}
