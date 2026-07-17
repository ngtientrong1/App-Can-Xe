using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.Views.Admin;

namespace CanXe.Desktop.Tests;

public sealed class Phase8Rc2AdminDialogLayoutTests
{
    private static string ResolveDesktopFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void AdminUnlock_UsesSizeToContent_NotFixedLowHeight()
    {
        var xaml = File.ReadAllText(ResolveDesktopFile(Path.Combine("Views", "Admin", "AdminUnlockWindow.xaml")));
        Assert.Contains("SizeToContent=\"Height\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"300\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"440\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Height=\"220\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsDefault=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsCancel=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"44\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualWeigh_UsesSizeToContent()
    {
        var xaml = File.ReadAllText(ResolveDesktopFile(Path.Combine("Views", "Admin", "ManualWeighWindow.xaml")));
        Assert.Contains("SizeToContent=\"Height\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Height=\"320\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Desktop_Version_Is_1_0_2()
    {
        var csproj = File.ReadAllText(ResolveDesktopFile("CanXe.Desktop.csproj"));
        Assert.Contains("<Version>1.0.3</Version>", csproj, StringComparison.Ordinal);
        Assert.Contains("<InformationalVersion>1.0.3</InformationalVersion>", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void InnoSetup_Is_1_0_2()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var iss = Path.Combine(dir.FullName, "installer", "CanXeTienTrong.iss");
            if (File.Exists(iss))
            {
                var text = File.ReadAllText(iss);
                Assert.Contains("#define MyAppVersion \"1.0.3\"", text, StringComparison.Ordinal);
                Assert.Contains("CanXeTienTrong-Setup-1.0.3", text, StringComparison.Ordinal);
                return;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("CanXeTienTrong.iss");
    }
}

[Collection("WpfSta")]
public sealed class Phase8Rc2AdminDialogWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase8Rc2AdminDialogWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AdminUnlock_ButtonsFullyVisible_WithoutClipping()
    {
        await _fixture.InvokeAsync(_ =>
        {
            var window = new AdminUnlockWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000,
                Top = -10000
            };
            window.Show();
            window.UpdateLayout();

            Assert.True(window.ActualHeight >= 280, $"Window too short: {window.ActualHeight}");
            Assert.True(window.ActualWidth >= 420, $"Window too narrow: {window.ActualWidth}");

            var buttons = EnumerateVisualTree(window).OfType<Button>().ToList();
            var unlock = buttons.First(b => Equals(b.Content, "Mở khóa"));
            var cancel = buttons.First(b => Equals(b.Content, "Hủy"));

            Assert.True(unlock.ActualHeight >= 40, $"Unlock button clipped: {unlock.ActualHeight}");
            Assert.True(cancel.ActualHeight >= 40, $"Cancel button clipped: {cancel.ActualHeight}");
            Assert.True(unlock.IsVisible);
            Assert.True(cancel.IsVisible);

            var unlockBottom = unlock.TransformToAncestor(window).Transform(new Point(0, unlock.ActualHeight)).Y;
            Assert.True(unlockBottom <= window.ActualHeight - 8,
                $"Unlock button bottom {unlockBottom} exceeds window height {window.ActualHeight}");

            window.Close();
            return Task.CompletedTask;
        });
    }

    private static IEnumerable<DependencyObject> EnumerateVisualTree(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in EnumerateVisualTree(child))
                yield return descendant;
        }
    }
}
