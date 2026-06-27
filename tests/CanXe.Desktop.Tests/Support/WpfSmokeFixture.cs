using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace CanXe.Desktop.Tests.Support;

public sealed class WpfSmokeFixture : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private WpfApplication? _app;
    private Exception? _initFailure;

    public WpfSmokeFixture()
    {
        _thread = new Thread(() =>
        {
            try
            {
                _app = new WpfApplication { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var resources = new ResourceDictionary();
                resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/CanXe.Desktop;component/Themes/CanXeDesignSystem.xaml", UriKind.Relative)
                });
                resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/CanXe.Desktop;component/Controls/AutoCompleteTextBox.xaml", UriKind.Relative)
                });
                _app.Resources = resources;
                _ready.Set();
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                _initFailure = ex;
                _ready.Set();
            }
        })
        {
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(30));
        if (_initFailure is not null)
            throw new InvalidOperationException("Failed to initialize WPF smoke fixture.", _initFailure);
        if (_app is null)
            throw new InvalidOperationException("WPF smoke fixture did not create Application.");
    }

    public void Invoke(Action<WpfApplication> action)
    {
        if (_app is null)
            throw new InvalidOperationException("WPF smoke fixture is not available.");

        _app.Dispatcher.Invoke(() => action(_app));
    }

    public T Invoke<T>(Func<WpfApplication, T> func)
    {
        if (_app is null)
            throw new InvalidOperationException("WPF smoke fixture is not available.");

        return _app.Dispatcher.Invoke(() => func(_app));
    }

    public void Dispose()
    {
        if (_app is null)
            return;

        _app.Dispatcher.InvokeShutdown();
        _thread.Join(TimeSpan.FromSeconds(10));
    }
}
