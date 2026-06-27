using System.IO;
using System.Text.RegularExpressions;
using CanXe.Application.Configuration;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public partial class MainWindowDevDiagnosticsBindingSmokeTests(WpfSmokeFixture wpf)
{
    private static string MainWindowXamlPath => ResolveMainWindowXamlPath();

    private static string ResolveMainWindowXamlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", "MainWindow.xaml");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate src/CanXe.Desktop/MainWindow.xaml.");
    }

    private static readonly string[] DevDiagnosticDisplayProperties =
    [
        "DevWeightSourceDisplay",
        "DevActiveScaleInputModeDisplay",
        "DevDeviceModeDisplay",
        "DevComConnectedDisplay",
        "DevComPortBaudDisplay",
        "DevFrameStatusDisplay",
        "DevStableDisplay",
        "DevLastFrameDisplay",
        "DevLastValidReadingTimeDisplay",
        "DevSimulationRunningDisplay"
    ];

    [Fact]
    public void DevDiagnosticsRunBindings_AreExplicitlyOneWay()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        foreach (var property in DevDiagnosticDisplayProperties)
        {
            var matches = DevRunBindingRegex().Matches(xaml)
                .Where(m => m.Groups["property"].Value == property)
                .ToList();

            Assert.NotEmpty(matches);
            Assert.All(matches, match =>
            {
                Assert.Contains("Mode=OneWay", match.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("Mode=TwoWay", match.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("OneWayToSource", match.Value, StringComparison.Ordinal);
            });
        }
    }

    [Fact]
    public void DevWeightSourceDisplay_HasNoTwoWayBindingInMainWindowXaml()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var matches = DevRunBindingRegex().Matches(xaml)
            .Where(m => m.Groups["property"].Value == "DevWeightSourceDisplay")
            .ToList();

        Assert.Single(matches);
        Assert.Contains("Mode=OneWay", matches[0].Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MainWindow_Show_WithFullViewModel_DoesNotThrow(bool developerMode)
    {
        await using var host = new DesktopTestHost(new AppSettings
        {
            DeviceMode = "Hardware",
            DeveloperMode = developerMode
        });

        var (viewModel, _) = await host.CreateMainViewModelForBindingSmokeAsync();

        wpf.Invoke(_ =>
        {
            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = viewModel
            };

            window.Show();
            WpfDispatcherPump.Pump(window.Dispatcher);
            Assert.True(window.IsLoaded);
            window.Close();
        });
    }

    [Fact]
    public async Task DevDrawer_Open_WithDeveloperMode_DoesNotThrow()
    {
        await using var host = new DesktopTestHost(new AppSettings
        {
            DeviceMode = "Hardware",
            DeveloperMode = true
        });

        var (viewModel, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        viewModel.IsDevDrawerOpen = true;

        wpf.Invoke(_ =>
        {
            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = viewModel
            };

            window.Show();
            WpfDispatcherPump.Pump(window.Dispatcher, passes: 5);
            Assert.True(viewModel.IsDevDrawerOpen);
            Assert.True(viewModel.IsDeveloperPanelAvailable);
            window.Close();
        });
    }

    [Fact]
    public async Task ManualSimulationPanel_IsVisible_WhenDeveloperModeAndManualSelected()
    {
        await using var host = new DesktopTestHost(new AppSettings
        {
            DeviceMode = "Hardware",
            DeveloperMode = true
        });

        var (viewModel, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        viewModel.IsDevDrawerOpen = true;

        wpf.Invoke(_ =>
        {
            viewModel.IsManualSimulationMode = true;
            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = viewModel
            };

            window.Show();
            WpfDispatcherPump.Pump(window.Dispatcher, passes: 5);
            Assert.True(viewModel.IsManualWeightInputVisible);
            window.Close();
        });
    }

    [GeneratedRegex(@"Run Text=""\{Binding (?<property>Dev\w+Display)(?<binding>[^""]*)""", RegexOptions.CultureInvariant)]
    private static partial Regex DevRunBindingRegex();
}
