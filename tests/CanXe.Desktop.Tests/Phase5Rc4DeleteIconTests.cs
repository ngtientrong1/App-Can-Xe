using System.IO;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

public sealed class Phase5Rc4DeleteIconTests
{
    private static string MainWindowXamlPath => ResolveMainWindowXamlPath();

    private static string DesignSystemXamlPath => ResolveDesignSystemXamlPath();

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

        throw new FileNotFoundException("Could not locate MainWindow.xaml.");
    }

    internal static string ResolveDesignSystemXamlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", "Themes", "CanXeDesignSystem.xaml");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate CanXeDesignSystem.xaml.");
    }

    [Fact]
    public void DeleteColumn_UsesTrashIconStyle_NotTextButton()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        Assert.Contains("CanXeHistoryDeleteButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("CanXeTrashIconGeometry", File.ReadAllText(DesignSystemXamlPath), StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"XÓA\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteColumn_HasCompactWidth()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        Assert.Contains("Width=\"64\"", xaml, StringComparison.Ordinal);
    }
}
