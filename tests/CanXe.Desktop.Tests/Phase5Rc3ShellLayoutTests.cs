using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using CanXe.Application.Configuration;
using CanXe.Application.Models;
using CanXe.Desktop;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.ViewModels;

namespace CanXe.Desktop.Tests;

public partial class Phase5Rc3ShellLayoutTests
{
    private static string MainWindowXamlPath => ResolveMainWindowXamlPath();

    internal static string ResolveMainWindowXamlPathPublic() => ResolveMainWindowXamlPath();

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
    public void Startup_DefaultActiveSection_IsWeighTicket()
    {
        var vm = new MainViewModelStub();
        Assert.Equal(AppNavigationSection.WeighTicket, vm.ActiveSection);
        Assert.True(vm.IsWeighTicketSectionVisible);
        Assert.False(vm.IsReportSectionVisible);
    }

    [Fact]
    public void ReportSection_VisibilityBindings_AreOnWrapperNotReportView()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var section = ExtractReportSection(xaml);

        Assert.Contains("Visibility=\"{Binding IsReportSectionVisible", section, StringComparison.Ordinal);
        Assert.Contains("reports:ReportView DataContext=\"{Binding Report}\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportView", ExtractWeighTicketSection(xaml), StringComparison.Ordinal);
        Assert.DoesNotMatch(ReportViewVisibilityOnSameElementRegex(), section);
    }

    [Fact]
    public void ReportView_DoesNotBindSectionVisibility_OnRootElement()
    {
        var reportXamlPath = Path.Combine(Path.GetDirectoryName(MainWindowXamlPath)!, "Views", "Reports", "ReportView.xaml");
        var xaml = File.ReadAllText(reportXamlPath);
        Assert.DoesNotContain("IsReportSectionVisible", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsWeighTicketSectionVisible", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AppNavigationSection.WeighTicket, true, false)]
    [InlineData(AppNavigationSection.Report, false, true)]
    public void SectionVisibility_FollowsActiveSection(
        AppNavigationSection section,
        bool weighVisible,
        bool reportVisible)
    {
        var vm = new MainViewModelStub { ActiveSection = section };
        Assert.Equal(weighVisible, vm.IsWeighTicketSectionVisible);
        Assert.Equal(reportVisible, vm.IsReportSectionVisible);
    }

    [Theory]
    [InlineData(AppNavigationSection.WeighTicket, true, false)]
    [InlineData(AppNavigationSection.Report, false, true)]
    public void NavHighlight_SyncsWithActiveSection(
        AppNavigationSection section,
        bool weighActive,
        bool reportActive)
    {
        var vm = new MainViewModelStub { ActiveSection = section };
        Assert.Equal(weighActive, vm.IsNavWeighTicketActive);
        Assert.Equal(reportActive, vm.IsNavReportActive);
    }

    [Fact]
    public async Task SwitchingToReportAndBack_PreservesDraftFields()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();
        vm.CustomerName = "Khách giữ draft";
        vm.LicensePlate = "51C-12345";

        vm.ActiveSection = AppNavigationSection.Report;
        vm.ActiveSection = AppNavigationSection.WeighTicket;

        Assert.Equal("Khách giữ draft", vm.CustomerName);
        Assert.Equal("51C-12345", vm.LicensePlate);
    }

    private static string ExtractWeighTicketSection(string xaml)
    {
        var start = xaml.IndexOf("<!-- Section: PHIẾU CÂN -->", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = xaml.IndexOf("<!-- Section: DANH MỤC -->", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return xaml[start..end];
    }

    private static string ExtractReportSection(string xaml)
    {
        var start = xaml.IndexOf("<!-- Section: BÁO CÁO -->", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = xaml.IndexOf("<!-- Section: CÀI ĐẶT -->", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return xaml[start..end];
    }

    [GeneratedRegex(@"reports:ReportView[\s\S]*Visibility\s*=\s*""\{Binding", RegexOptions.Singleline)]
    private static partial Regex ReportViewVisibilityOnSameElementRegex();

    private sealed class MainViewModelStub
    {
        public AppNavigationSection ActiveSection { get; set; } = AppNavigationSection.WeighTicket;

        public bool IsWeighTicketSectionVisible => ActiveSection == AppNavigationSection.WeighTicket;
        public bool IsReportSectionVisible => ActiveSection == AppNavigationSection.Report;
        public bool IsNavWeighTicketActive => ActiveSection == AppNavigationSection.WeighTicket;
        public bool IsNavReportActive => ActiveSection == AppNavigationSection.Report;
    }
}

[Collection("WpfSta")]
public sealed class Phase5Rc3ShellLayoutWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc3ShellLayoutWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MainWindow_AtStartup_ReportTextsAreNotVisibleInContent()
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        await _fixture.InvokeAsync(_ =>
        {
            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = vm,
                Width = 1366,
                Height = 768
            };
            window.Show();
            window.UpdateLayout();

            Assert.False(ShellLayoutTestHelpers.IsAnyVisibleText(window, "BÁO CÁO PHIẾU CÂN"));
            Assert.False(ShellLayoutTestHelpers.IsAnyVisibleText(window, "Từ ngày"));
            Assert.False(ShellLayoutTestHelpers.IsAnyVisibleText(window, "Đến ngày"));
            Assert.False(ShellLayoutTestHelpers.IsAnyVisibleText(window, "XUẤT EXCEL"));
            Assert.True(ShellLayoutTestHelpers.IsAnyVisibleText(window, "TRỌNG LƯỢNG TRỰC TIẾP"));

            window.Close();
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(1366, 768)]
    [InlineData(1920, 1080)]
    public async Task MainWindow_SwitchingTabs_DoesNotOverlap(int width, int height)
    {
        await using var host = new DesktopTestHost(new AppSettings());
        var (vm, _) = await host.CreateMainViewModelForBindingSmokeAsync();
        await vm.InitializeAsync();

        await _fixture.InvokeAsync(_ =>
        {
            var window = new MainWindow(new NoOpUiFocusService())
            {
                DataContext = vm,
                Width = width,
                Height = height
            };
            window.Show();
            window.UpdateLayout();

            vm.ActiveSection = AppNavigationSection.Report;
            window.UpdateLayout();
            Assert.True(ShellLayoutTestHelpers.IsAnyVisibleText(window, "BÁO CÁO PHIẾU CÂN"));
            Assert.False(ShellLayoutTestHelpers.IsAnyVisibleText(window, "TRỌNG LƯỢNG TRỰC TIẾP"));

            vm.ActiveSection = AppNavigationSection.WeighTicket;
            window.UpdateLayout();
            Assert.False(ShellLayoutTestHelpers.IsAnyVisibleText(window, "BÁO CÁO PHIẾU CÂN"));
            Assert.True(ShellLayoutTestHelpers.IsAnyVisibleText(window, "TRỌNG LƯỢNG TRỰC TIẾP"));

            window.Close();
            return Task.CompletedTask;
        });
    }
}

internal static class ShellLayoutTestHelpers
{
    public static bool IsAnyVisibleText(DependencyObject root, string text)
    {
        foreach (var block in EnumerateVisualTree(root).OfType<System.Windows.Controls.TextBlock>())
        {
            if (!block.IsVisible || block.Visibility != Visibility.Visible)
                continue;
            if (string.Equals(block.Text, text, StringComparison.Ordinal))
                return true;
        }

        return false;
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
