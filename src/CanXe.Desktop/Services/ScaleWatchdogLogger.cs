using CanXe.Infrastructure.Logging;

namespace CanXe.Desktop.Services;

/// <summary>
/// Scale reconnect / watchdog milestones (not per-frame).
/// Writes to lifecycle.log and scale-connection.log.
/// </summary>
public static class ScaleWatchdogLogger
{
    public static void Write(string eventName, string? detail = null)
    {
        LifecycleLogger.Write(eventName, detail);
        try
        {
            Infrastructure.Scale.ScaleConnectionLogger.Write(eventName, detail);
        }
        catch
        {
            // Best effort.
        }
    }
}
