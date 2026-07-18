using System.Windows.Threading;

namespace CanXe.Desktop.Services;

/// <summary>
/// Marshals work to the UI thread without blocking background threads.
/// Blocking Dispatcher.Invoke during SerialPort close causes shutdown deadlocks.
/// </summary>
public static class UiThreadMarshal
{
    public static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return;

        if (dispatcher.CheckAccess())
        {
            try
            {
                action();
            }
            catch
            {
                // Best effort during teardown.
            }

            return;
        }

        try
        {
            _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
        }
        catch (Exception)
        {
            // Dispatcher may already be shutting down.
        }
    }
}
