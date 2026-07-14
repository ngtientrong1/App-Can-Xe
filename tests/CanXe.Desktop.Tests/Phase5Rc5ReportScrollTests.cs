using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CanXe.Application.Configuration;
using CanXe.Application.Models;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.Views.Reports;

namespace CanXe.Desktop.Tests;

public sealed class Phase5Rc5ReportScrollTests
{
    private static string ReportViewXamlPath => ResolveReportViewXamlPath();

    private static string ResolveReportViewXamlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "CanXe.Desktop", "Views", "Reports", "ReportView.xaml");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate ReportView.xaml.");
    }

    [Fact]
    public void ReportView_DoesNotWrapDataGridInOuterScrollViewer()
    {
        var xaml = File.ReadAllText(ReportViewXamlPath);
        Assert.DoesNotContain("<ScrollViewer", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportView_DataGrid_HasStarRowAndVirtualization()
    {
        var xaml = File.ReadAllText(ReportViewXamlPath);
        Assert.Contains("Height=\"*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"3\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Trạng thái\"", xaml, StringComparison.Ordinal);
        Assert.Contains("EnableRowVirtualization=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewMouseWheel=\"ReportDataGrid_OnPreviewMouseWheel\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportView_UsesVietnameseNumberConverter_ForGridNumbers()
    {
        var xaml = File.ReadAllText(ReportViewXamlPath);
        Assert.Contains("VnNumberConverter", xaml, StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=weight", xaml, StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=money", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StringFormat=N0", xaml, StringComparison.Ordinal);
    }
}

[Collection("WpfSta")]
public sealed class Phase5Rc5ReportScrollWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc5ReportScrollWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(1366, 768)]
    [InlineData(1920, 1080)]
    public async Task ReportDataGrid_ShowsVerticalScrollbar_AndScrollsWithWheel(int width, int height)
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        vm.ActiveSection = AppNavigationSection.Report;
        await vm.Report.InitializeAsync();

        await _fixture.InvokeAsync(_ =>
        {
            var view = new ReportView { DataContext = vm.Report, Width = width, Height = height };
            var window = new Window
            {
                Content = view,
                Width = width,
                Height = height
            };
            window.Show();
            window.UpdateLayout();

            var grid = FindVisualChild<DataGrid>(view);
            Assert.NotNull(grid);
            Assert.True(grid!.EnableRowVirtualization);
            Assert.True(VirtualizingPanel.GetIsVirtualizing(grid));

            var scrollViewer = FindVisualChild<ScrollViewer>(grid);
            Assert.NotNull(scrollViewer);

            for (var i = 0; i < 40; i++)
                vm.Report.Rows.Add(new ReportRowDto
                {
                    DisplayNumber = $"06/{i:00}",
                    TicketDateTime = DateTimeOffset.Now,
                    GrossWeightKg = 9000 + i,
                    NetWeightKg = 1000 + i
                });
            window.UpdateLayout();

            Assert.True(scrollViewer!.ComputedVerticalScrollBarVisibility == Visibility.Visible
                        || scrollViewer.ScrollableHeight > 0);

            var before = scrollViewer.VerticalOffset;
            grid.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -240)
            {
                RoutedEvent = UIElement.PreviewMouseWheelEvent,
                Source = grid
            });
            window.UpdateLayout();
            Assert.True(scrollViewer.VerticalOffset > before);

            window.Close();
            return Task.CompletedTask;
        });
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
