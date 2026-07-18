namespace CanXe.Domain.Services;

/// <summary>
/// Policy for port-open vs data-alive and auto COM reconnect backoff (1.0.6).
/// </summary>
public static class ScaleWatchdogPolicy
{
    /// <summary>Valid frame must be newer than this to count as data-alive.</summary>
    public static TimeSpan DataAliveWindow { get; } = TimeSpan.FromSeconds(3);

    /// <summary>Port open without a valid frame for this long triggers auto reconnect.</summary>
    public static TimeSpan NoDataTimeout { get; } = TimeSpan.FromSeconds(6);

    /// <summary>Watchdog poll interval (non-blocking).</summary>
    public static TimeSpan PollInterval { get; } = TimeSpan.FromSeconds(1);

    /// <summary>Brief settle delay between disconnect and reconnect.</summary>
    public static TimeSpan ReconnectSettleDelay { get; } = TimeSpan.FromMilliseconds(400);

    public static bool IsDataAlive(DateTimeOffset? lastValidFrameAt, DateTimeOffset now) =>
        lastValidFrameAt is not null
        && now - lastValidFrameAt.Value <= DataAliveWindow;

    public static bool ShouldTriggerNoDataTimeout(
        bool isShuttingDown,
        bool disconnectedByUser,
        bool isHardwareMode,
        bool isReconnectInProgress,
        bool portOpen,
        bool isDataAlive,
        DateTimeOffset? waitingForDataSince,
        DateTimeOffset now)
    {
        if (isShuttingDown || disconnectedByUser || !isHardwareMode || isReconnectInProgress)
            return false;
        if (!portOpen || isDataAlive)
            return false;
        if (waitingForDataSince is null)
            return false;
        return now - waitingForDataSince.Value >= NoDataTimeout;
    }

    public static bool ShouldReconnectWhileDataAlive(
        bool isDataAlive,
        bool isShuttingDown) =>
        !isShuttingDown && isDataAlive;

    /// <summary>
    /// Backoff before the Nth auto-reconnect attempt (0-based).
    /// First attempt: immediate after NoDataTimeout; then 2s, 5s, 10s, 30s max.
    /// </summary>
    public static TimeSpan GetReconnectBackoff(int retryIndex) =>
        retryIndex switch
        {
            <= 0 => TimeSpan.Zero,
            1 => TimeSpan.FromSeconds(2),
            2 => TimeSpan.FromSeconds(5),
            3 => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(30)
        };

    public static string FormatWaitingForDataStatus(string portName, int baudRate) =>
        $"Đã kết nối {portName} @ {baudRate} — đang chờ dữ liệu";

    public static string FormatConnectedStatus(string portName, int baudRate) =>
        $"Đã kết nối {portName} @ {baudRate}";
}
