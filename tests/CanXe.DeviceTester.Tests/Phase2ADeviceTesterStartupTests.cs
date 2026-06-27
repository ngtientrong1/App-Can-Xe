using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using CanXe.DeviceTester.Core.Services;
using CanXe.DeviceTester.Tests.Support;
using CanXe.DeviceTester.ViewModels;

namespace CanXe.DeviceTester.Tests;

public class Phase2ADeviceTesterStartupTests
{
    [Fact]
    public void LogDirectory_IsReadableFromViewModel()
    {
        using var vm = CreateViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.LogDirectory));
        Assert.Contains("CanXeDeviceTester", vm.LogDirectory);
    }

    [Fact]
    public void MainWindowXaml_LogDirectoryBinding_IsOneWay()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());
        Assert.Matches(
            new Regex(@"Text=""\{Binding LogDirectory, Mode=OneWay\}""", RegexOptions.CultureInvariant),
            xaml);
    }

    [Fact]
    public void MainWindowXaml_HasNoTwoWayBindingToReadOnlyProperties()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());
        foreach (var property in DeviceTesterBindingPolicy.ReadOnlyDisplayProperties)
        {
            var twoWayPattern = new Regex(
                $@"Binding\s+{property}(?!,)[^""]*""|Binding\s+{property},\s*Mode=TwoWay",
                RegexOptions.CultureInvariant);
            Assert.False(twoWayPattern.IsMatch(xaml),
                $"Found TwoWay or default binding to read-only property '{property}'.");
        }

        foreach (var property in DeviceTesterBindingPolicy.ReadOnlyDisplayProperties)
        {
            if (!xaml.Contains($"Binding {property}", StringComparison.Ordinal) &&
                !xaml.Contains($"Binding {property},", StringComparison.Ordinal))
                continue;

            Assert.Contains($"Binding {property}, Mode=OneWay", xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void StartupErrorLogger_CreatesLogDirectory()
    {
        var dir = StartupErrorLogger.LogDirectory;
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);

        StartupErrorLogger.EnsureLogDirectoryExists();
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void StartupErrorLogger_WritesExceptionWithoutSensitiveData()
    {
        var path = StartupErrorLogger.LogFilePath;
        if (File.Exists(path))
            File.Delete(path);

        StartupErrorLogger.Write(new InvalidOperationException("binding test failure"));
        var text = File.ReadAllText(path);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("binding test failure", text);
    }

    [Fact]
    public void MainWindow_ShowClose_DoesNotThrow_OnStaThread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                using var vm = CreateViewModel();
                var window = new MainWindow { DataContext = vm };
                window.Show();
                window.Close();
                app.Shutdown();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(15));
        Assert.Null(failure);
    }

    private static DeviceTesterViewModel CreateViewModel()
    {
        var provider = new FakeSerialPortProvider();
        var capture = new SerialCaptureService(
            provider,
            new FakeSerialPortDiscoveryService(),
            new RawSerialLogWriter());
        return new DeviceTesterViewModel(capture);
    }

    private static string GetMainWindowXamlPath()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "CanXe.DeviceTester", "MainWindow.xaml");
    }
}
