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

        // Outer PHIẾU CÂN rows: workspace Auto…; ticket list *. Nested spacer "*"
        // inside the weigh card (Admin layout) is allowed.
        var marker = "IsWeighTicketSectionVisible";
        var markerAt = section.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerAt >= 0);
        var rowsOpen = section.IndexOf("<Grid.RowDefinitions>", markerAt, StringComparison.Ordinal);
        var rowsClose = section.IndexOf("</Grid.RowDefinitions>", rowsOpen, StringComparison.Ordinal);
        Assert.True(rowsOpen > markerAt && rowsClose > rowsOpen);
        var outerRowsXml = section[(rowsOpen + "<Grid.RowDefinitions>".Length)..rowsClose];

        var rowHeights = Regex.Matches(outerRowsXml, @"Height=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.True(rowHeights.Count >= 5, "Expected at least 5 outer rows.");
        Assert.Equal("Auto", rowHeights[0]);
        Assert.Equal("*", rowHeights[4]);
        Assert.Contains("x:Name=\"TicketsGrid\" Grid.Row=\"4\"", section, StringComparison.Ordinal);
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
    public void WeighTicketSection_IsWrappedInUniformViewbox()
    {
        // The whole PHIẾU CÂN screen renders onto a fixed 16:9 design canvas that a
        // Viewbox scales uniformly to the actual window — so every proportion (fonts,
        // paddings, column widths) stays identical at any resolution/DPI instead of
        // jumping between discrete size breakpoints.
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);

        Assert.Contains("<Viewbox", section, StringComparison.Ordinal);
        Assert.Contains("Stretch=\"Uniform\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void WeighTicketSection_WorkspaceUsesFixedDesignHeights()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);

        Assert.Contains("MaxHeight=\"510\"", section, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"420\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void WeighTicketSection_DataGridUsesFixedDesignMinHeight()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractWeighTicketSection(xaml);

        Assert.Contains("MinHeight=\"280\"", section, StringComparison.Ordinal);
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
