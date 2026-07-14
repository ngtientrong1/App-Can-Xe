using System.IO;
using System.Windows;
using CanXe.Application.Configuration;
using CanXe.Application.Models;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

public sealed class Phase5Rc6CatalogShellTests
{
    private static string MainWindowXamlPath => Phase5Rc3ShellLayoutTests.ResolveMainWindowXamlPathPublic();

    [Fact]
    public void CatalogSection_UsesIsolatedCatalogViewWrapper()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);
        var start = xaml.IndexOf("<!-- Section: DANH MỤC -->", StringComparison.Ordinal);
        var end = xaml.IndexOf("<!-- Section: THIẾT BỊ -->", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var section = xaml[start..end];

        Assert.Contains("Visibility=\"{Binding IsCatalogSectionVisible", section, StringComparison.Ordinal);
        Assert.Contains("catalogs:CatalogView DataContext=\"{Binding Catalog}\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("giai đoạn tiếp theo", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogView_DoesNotBindSectionVisibility_OnRootElement()
    {
        var path = Path.Combine(Path.GetDirectoryName(MainWindowXamlPath)!, "Views", "Catalogs", "CatalogView.xaml");
        var xaml = File.ReadAllText(path);
        Assert.DoesNotContain("IsCatalogSectionVisible", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsWeighTicketSectionVisible", xaml, StringComparison.Ordinal);
    }
}

[Collection("WpfSta")]
public sealed class Phase5Rc6CatalogShellWpfTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase5Rc6CatalogShellWpfTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(1366, 768)]
    [InlineData(1920, 1080)]
    public async Task SwitchingToCatalog_DoesNotOverlapWeighTicket(int width, int height)
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

            vm.ActiveSection = AppNavigationSection.Catalog;
            window.UpdateLayout();
            Assert.True(ShellLayoutTestHelpers.IsAnyVisibleText(window, "Tên khách hàng"));
            Assert.False(ShellLayoutTestHelpers.IsAnyVisibleText(window, "TRỌNG LƯỢNG TRỰC TIẾP"));

            vm.ActiveSection = AppNavigationSection.WeighTicket;
            window.UpdateLayout();
            Assert.False(ShellLayoutTestHelpers.IsAnyVisibleText(window, "Tên khách hàng"));
            Assert.True(ShellLayoutTestHelpers.IsAnyVisibleText(window, "TRỌNG LƯỢNG TRỰC TIẾP"));

            window.Close();
            return Task.CompletedTask;
        });
    }

}
