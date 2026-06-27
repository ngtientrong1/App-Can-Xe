using System.Windows.Threading;

namespace CanXe.Desktop.Tests.Support;

internal static class WpfDispatcherPump
{
    public static void Pump(Dispatcher dispatcher, int passes = 3)
    {
        for (var i = 0; i < passes; i++)
        {
            dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
            dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
    }
}
