using System.IO;
using System.Text.RegularExpressions;

namespace CanXe.Desktop.Tests;

public partial class MainWindowLayoutStructureTests
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

    [Fact]
    public void WeighTicketSection_DataGridRow_UsesStarHeight()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);

        Assert.Contains("Height=\"*\"", section, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TicketsGrid\" Grid.Row=\"4\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void WeighTicketSection_WorkspaceRow_DoesNotUseStarHeight()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);
        var workspaceRow = Regex.Match(section, @"<Grid Grid\.Row=""0""[\s\S]*?</Grid>", RegexOptions.Singleline);

        Assert.True(workspaceRow.Success);
        Assert.DoesNotContain("Height=\"*\"", workspaceRow.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void WeighTicketSection_MainContentHasNoOuterVerticalScrollViewer()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);
        var withoutDevDrawer = Regex.Replace(
            section,
            @"<!-- DEV drawer -->[\s\S]*?</Border>\s*",
            string.Empty,
            RegexOptions.Singleline);

        Assert.DoesNotContain("<ScrollViewer", withoutDevDrawer, StringComparison.Ordinal);
    }

    [Fact]
    public void WeighTicketSection_WorkspaceUsesMaxHeightBinding()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);

        Assert.Contains("MaxHeight=\"{Binding WorkspaceMaxHeight}\"", section, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{Binding WorkspaceMinHeight}\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void WeighTicketSection_DataGridUsesMinHeightBinding()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);

        Assert.Contains("MinHeight=\"{Binding DataGridMinHeight}\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void WeighTicketSection_FooterUsesCompactSummaryHeight()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);

        Assert.Contains("MaxHeight=\"{Binding SummaryFooterMaxHeight}\"", section, StringComparison.Ordinal);
        Assert.Contains("CanXeCompactSummaryCardStyle", section, StringComparison.Ordinal);
    }

    private static string ExtractWeighTicketSection(string xaml)
    {
        var start = xaml.IndexOf("<!-- Section: PHIẾU CÂN -->", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = xaml.IndexOf("<!-- Section: DANH MỤC -->", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return xaml[start..end];
    }
}
