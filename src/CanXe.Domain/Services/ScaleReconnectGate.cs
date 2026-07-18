namespace CanXe.Domain.Services;

/// <summary>
/// Prevents concurrent scale reconnect attempts (manual + watchdog share this gate).
/// </summary>
public sealed class ScaleReconnectGate
{
    private int _inProgress;

    public bool IsInProgress => Volatile.Read(ref _inProgress) != 0;

    public bool TryEnter() => Interlocked.CompareExchange(ref _inProgress, 1, 0) == 0;

    public void Exit() => Interlocked.Exchange(ref _inProgress, 0);
}
