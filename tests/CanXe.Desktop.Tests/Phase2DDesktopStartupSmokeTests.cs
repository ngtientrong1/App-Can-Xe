using System.IO;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public class Phase2DDesktopStartupSmokeTests(WpfSmokeFixture wpf)
{
    private static readonly string[] RequiredResourceKeys =
    [
        "SectionHeaderTextStyle",
        "CanXeFormFieldLabelStyle",
        "CanXeTextBoxStyle",
        "CanXeCardStyle",
        "CanXeNavigationButtonStyle",
        "PageBackgroundBrush",
        "CanXePrimaryButtonStyle",
        "CanXeSecondaryButtonStyle",
        "RecordWeightButton",
        "SaveButton",
        "CancelButton",
        "ActionButton",
        "CanXeSummaryCardStyle"
    ];

    [Fact]
    public void AppResources_LoadSuccessfully()
    {
        wpf.Invoke(app =>
        {
            Assert.NotNull(app.Resources);
            Assert.Equal(2, app.Resources.MergedDictionaries.Count);
        });
    }

    [Fact]
    public void MergedDictionaries_LoadWithoutXamlParseException()
    {
        wpf.Invoke(app =>
        {
            foreach (var dictionary in app.Resources.MergedDictionaries)
                Assert.NotNull(dictionary);
        });
    }

    [Fact]
    public void SectionHeaderTextStyle_Exists_AndLegacySectionHeaderRemoved()
    {
        wpf.Invoke(app =>
        {
            Assert.NotNull(app.Resources["SectionHeaderTextStyle"]);
            Assert.Null(app.Resources["SectionHeader"]);
        });
    }

    [Theory]
    [MemberData(nameof(RequiredResourceKeysData))]
    public void Phase2D_StaticResources_Exist(string resourceKey)
    {
        wpf.Invoke(app =>
        {
            Assert.True(
                app.Resources.Contains(resourceKey),
                $"Missing StaticResource key '{resourceKey}'.");
        });
    }

    public static IEnumerable<object[]> RequiredResourceKeysData() =>
        RequiredResourceKeys.Select(key => new object[] { key });

    [Fact]
    public void MainWindow_CreatesAndCloses_OnStaThread()
    {
        wpf.Invoke(_ =>
        {
            var window = new MainWindow(new NoOpUiFocusService());
            window.Show();
            window.Close();
        });
    }

    [Fact]
    public void MainWindow_LoadsWithNullDataContext_ForHardwareLayout()
    {
        wpf.Invoke(_ =>
        {
            var window = new MainWindow(new NoOpUiFocusService()) { DataContext = null };
            window.Show();
            Assert.True(window.IsLoaded);
            window.Close();
        });
    }

    [Fact]
    public void MainWindow_LoadsWithNullDataContext_ForSimulationLayout()
    {
        wpf.Invoke(_ =>
        {
            var window = new MainWindow(new NoOpUiFocusService()) { DataContext = null };
            window.Show();
            Assert.True(window.IsLoaded);
            window.Close();
        });
    }

    [Fact]
    public void StartupErrorLogger_WritesExceptionWithVersionAndOs()
    {
        var path = StartupErrorLogger.LogFilePath;
        if (File.Exists(path))
            File.Delete(path);

        StartupErrorLogger.Write(new InvalidOperationException("desktop startup smoke test"));
        var text = File.ReadAllText(path);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("App version:", text);
        Assert.Contains("Windows:", text);
    }
}
