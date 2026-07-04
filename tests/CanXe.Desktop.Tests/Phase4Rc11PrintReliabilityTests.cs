using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc11PrintReliabilityTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc11PrintReliabilityTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void PreviewAndPrintDocuments_AreSeparateInstances()
    {
        _fixture.Invoke(_ =>
        {
            var factory = new WpfWeighTicketDocumentFactory();
            var model = BuildSampleModel(false);
            var previewDoc = factory.CreateDocument(model);
            var printDoc = factory.CreateDocument(model);
            Assert.NotSame(previewDoc, printDoc);

            var previewTop = GetTopCopy(previewDoc);
            var printTop = GetTopCopy(printDoc);
            Assert.NotSame(previewTop, printTop);
        });
    }

    [Fact]
    public void MaterializedCopy_HasDataContextAndPositiveSize()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel(false));
            Assert.NotNull(copy.DataContext);
            Assert.True(copy.ActualWidth > 0);
            Assert.True(copy.ActualHeight > 0);
            Assert.True(WpfWeighTicketDocumentFactory.CountDescendants(copy) > 20);
        });
    }

    [Fact]
    public void NormalTicket_DoesNotShowWatermark()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel(false));
            Assert.Null(FindTextBlock(copy, "BẢN IN LẠI"));
        });
    }

    [Fact]
    public void ReprintTicket_ShowsWatermarkAndMainContent()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel(true));
            Assert.NotNull(FindTextBlock(copy, "BẢN IN LẠI"));
            Assert.NotNull(FindTextBlock(copy, "PHIẾU CÂN XE"));
            Assert.NotNull(FindTextBlock(copy, "KHỐI LƯỢNG XE + HÀNG"));
        });
    }

    [Fact]
    public void RasterBitmap_MeetsNonWhiteThreshold()
    {
        _fixture.Invoke(_ =>
        {
            var renderer = new WeighTicketRasterPrintRenderer();
            var doc = renderer.CreateRasterDocument(BuildSampleModel(false), new WeighTicketPrintRenderingLogger(), out var ratio);
            Assert.True(ratio >= WeighTicketRasterPrintRenderer.MinNonWhitePixelRatio);
            var page = doc.Pages[0].GetPageRoot(false) as FixedPage;
            Assert.NotNull(page?.Children.OfType<Image>().FirstOrDefault());
        });
    }

    [Fact]
    public void IdentityIcons_UseWhiteFillGeometry()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel(false));
            var customerTile = FindNamed<Border>(copy, "CustomerIconTile");
            var path = FindFirstPath(customerTile!);
            Assert.Equal(Brushes.White, path!.Fill);
            Assert.Equal(Brushes.Transparent, path.Stroke);
        });
    }

    private static WeighTicketPrintModel BuildSampleModel(bool isReprint) =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = isReprint ? 2 : 0,
                DisplayNumber = "15/06",
                CustomerName = "Khách Hàng C",
                LicensePlate = "81C1234356",
                CargoTypeName = "Rổ nhãn",
                GrossWeightKg = 180m,
                TareWeightKg = 170m,
                NetWeightKg = 10m,
                UnitPriceVndPerKg = 80_000m,
                TotalAmountVnd = 800_000m,
                Weight1RecordedAt = new DateTimeOffset(2026, 6, 29, 12, 45, 33, TimeSpan.FromHours(7)),
                Weight2RecordedAt = new DateTimeOffset(2026, 6, 29, 12, 46, 7, TimeSpan.FromHours(7)),
                TicketDateTime = new DateTimeOffset(2026, 6, 29, 12, 46, 7, TimeSpan.FromHours(7))
            },
            new StationSettingsDto { StationName = "TRẠM CÂN TIẾN TRỌNG" },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp },
            isReprint);

    private static WeighTicketCopyView GetTopCopy(FixedDocument document)
    {
        var page = document.Pages[0].GetPageRoot(false) as FixedPage;
        return page!.Children.OfType<WeighTicketCopyView>().OrderBy(c => FixedPage.GetTop(c)).First();
    }

    private static TextBlock? FindTextBlock(DependencyObject root, string text)
    {
        if (root is TextBlock tb && tb.Text == text && tb.Visibility == Visibility.Visible)
            return tb;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindTextBlock(child, text);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    private static T? FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        if (root is T fe && fe.Name == name)
            return fe;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindNamed<T>(child, name);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    private static System.Windows.Shapes.Path? FindFirstPath(DependencyObject root)
    {
        if (root is System.Windows.Shapes.Path path)
            return path;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindFirstPath(child);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }
}
