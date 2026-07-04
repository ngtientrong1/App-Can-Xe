using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc6TicketVisualTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc6TicketVisualTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void WeighTicketCopyView_TitleUsesPtToDip24()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildRc6SampleModel());
            var title = FindFirstTextBlock(view, "PHIẾU CÂN XE");
            Assert.NotNull(title);
            Assert.Equal(HorizontalAlignment.Center, title!.HorizontalAlignment);
            Assert.Equal(TextAlignment.Center, title.TextAlignment);
            Assert.True(title.FontSize >= WeighTicketPrintTypography.TitleDip - 0.5);
        });
    }

    [Fact]
    public void WeighTicketCopyView_MainLabelUsesPtToDip()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildRc6SampleModel());
            var label = FindFirstTextBlock(view, "Loại hàng:");
            Assert.NotNull(label);
            Assert.True(label!.FontSize >= WeighTicketPrintTypography.DetailLabelDip - 0.5);
        });
    }

    [Fact]
    public void WeighTicketCopyView_WeighTimeUsesPtToDip15Point5()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildRc6SampleModel());
            var time = FindFirstTextBlock(view, "23:58:10");
            Assert.NotNull(time);
            Assert.True(time!.FontSize >= WeighTicketPrintTypography.WeighTimeDip - 0.5);
        });
    }

    [Fact]
    public void WeighTicketCopyView_RightPanelDoesNotExceedContentWidth()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildStressModel());
            var maxRight = GetSafeContentMaxRight(view);
            Assert.True(maxRight <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 0.5,
                $"Right edge {maxRight} exceeded {WeighTicketPrintLayout.CopySafeRightEdgeDip}");
        });
    }

    [Fact]
    public void WeighTicketCopyView_NoCopyLabelOrViewbox()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildRc6SampleModel());
            Assert.Null(FindFirstTextBlock(view, "LIÊN TRẠM CÂN"));
            Assert.Null(FindFirstTextBlock(view, "CẮT THEO ĐƯỜNG NÉT ĐỨT"));
            Assert.False(ContainsViewboxOutsideIconTiles(view));
            Assert.False(ContainsScaleTransform(view));
        });
    }

    [Fact]
    public void ExportRc6ActualAndComparisonArtifacts()
    {
        var paths = _fixture.Invoke(_ =>
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var artifactsDir = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifactsDir);

            var model = BuildRc6SampleModel();
            var document = WpfWeighTicketDocumentBuilder.BuildFixedDocument(model);
            var page = document.DocumentPaginator.GetPage(0);
            var width = page.Size.Width;
            var height = page.Size.Height;
            var visual = (UIElement)page.Visual;
            visual.Measure(new Size(width, height));
            visual.Arrange(new Rect(0, 0, width, height));
            visual.UpdateLayout();

            var actualPath = Path.Combine(artifactsDir, "phase4-rc6-actual-ticket.png");
            SavePng(visual, width, height, actualPath);

            var referencePath = Path.Combine(repoRoot, "tests", "TestAssets", "Printing", "approved-a5-ticket-reference.png");
            var comparisonPath = Path.Combine(artifactsDir, "phase4-rc6-reference-vs-actual.png");
            if (File.Exists(referencePath))
                SaveSideBySideComparison(referencePath, actualPath, comparisonPath);

            return (actualPath, comparisonPath);
        });

        Assert.True(File.Exists(paths.actualPath));
        Assert.True(new FileInfo(paths.actualPath).Length > 10_000);
    }

    private static WeighTicketCopyView ArrangeView(WeighTicketPrintModel model)
    {
        var view = new WeighTicketCopyView();
        view.Bind(model);
        view.Measure(new Size(WeighTicketPrintLayout.ContentUsableWidthDip, WeighTicketPrintLayout.ContentUsableHeightDip));
        view.Arrange(new Rect(0, 0, WeighTicketPrintLayout.ContentUsableWidthDip, WeighTicketPrintLayout.ContentUsableHeightDip));
        view.UpdateLayout();
        return view;
    }

    private static WeighTicketPrintModel BuildRc6SampleModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "68/06/NK",
                CustomerName = "Cân Dịch Vụ",
                LicensePlate = "82C08112",
                CargoTypeName = "Rơ tươi",
                Notes = "Giao buổi chiều",
                Weight1Kg = 11_730m,
                Weight2Kg = 4_980m,
                GrossWeightKg = 11_730m,
                TareWeightKg = 4_980m,
                NetWeightKg = 6_750m,
                UnitPriceVndPerKg = 89_000m,
                TotalAmountVnd = 600_750_000m,
                Weight1RecordedAt = new DateTimeOffset(2026, 6, 29, 23, 58, 10, TimeSpan.FromHours(7)),
                Weight2RecordedAt = new DateTimeOffset(2026, 6, 30, 0, 12, 5, TimeSpan.FromHours(7)),
                TicketDateTime = new DateTimeOffset(2026, 6, 30, 15, 30, 0, TimeSpan.FromHours(7))
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
                Address = "Thôn 7 Xã Ngọc Wang, Huyện Đăk Hà, Tỉnh Kon Tum",
                Phone = "0363261945",
                SignLocationName = "Kon Tum"
            },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });

    private static WeighTicketPrintModel BuildStressModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "999999/12/NK",
                CustomerName = "Cân Dịch Vụ",
                LicensePlate = "82C08112",
                CargoTypeName = "Rơ tươi",
                Weight1Kg = 11_730m,
                Weight2Kg = 4_980m,
                GrossWeightKg = 11_730m,
                TareWeightKg = 4_980m,
                NetWeightKg = 6_750m,
                UnitPriceVndPerKg = 89_000m,
                TotalAmountVnd = 600_750_000m,
                Weight1RecordedAt = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(7)),
                Weight2RecordedAt = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(7)),
                TicketDateTime = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(7))
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
                Address = "Thôn 7 Xã Ngọc Wang, Huyện Đăk Hà, Tỉnh Kon Tum",
                Phone = "0363261945",
                SignLocationName = "Kon Tum"
            },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });

    private static void SavePng(Visual visual, double width, double height, string path)
    {
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void SaveSideBySideComparison(string referencePath, string actualPath, string outputPath)
    {
        var reference = new BitmapImage();
        reference.BeginInit();
        reference.UriSource = new Uri(referencePath, UriKind.Absolute);
        reference.CacheOption = BitmapCacheOption.OnLoad;
        reference.EndInit();
        reference.Freeze();

        var actual = new BitmapImage();
        actual.BeginInit();
        actual.UriSource = new Uri(actualPath, UriKind.Absolute);
        actual.CacheOption = BitmapCacheOption.OnLoad;
        actual.EndInit();
        actual.Freeze();

        var refHeight = (int)reference.PixelHeight;
        var refWidth = (int)reference.PixelWidth;
        var actHeight = (int)actual.PixelHeight;
        var actWidth = (int)actual.PixelWidth;
        var height = Math.Max(refHeight, actHeight);
        var width = refWidth + actWidth + 20;

        var surface = new Grid
        {
            Width = width,
            Height = height,
            Background = Brushes.White
        };
        surface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(refWidth) });
        surface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        surface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(actWidth) });

        var left = new Image { Source = reference, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left };
        var right = new Image { Source = actual, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        surface.Children.Add(left);
        surface.Children.Add(right);

        surface.Measure(new Size(width, height));
        surface.Arrange(new Rect(0, 0, width, height));
        surface.UpdateLayout();
        SavePng(surface, width, height, outputPath);
    }

    private static TextBlock? FindFirstTextBlock(DependencyObject root, string text)
    {
        if (root is TextBlock tb && tb.Text == text)
            return tb;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindFirstTextBlock(child, text);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    private static bool ContainsType<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T)
            return true;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child && ContainsType<T>(child))
                return true;
        }
        return false;
    }

    private static bool ContainsViewboxOutsideIconTiles(DependencyObject root) =>
        ContainsViewboxOutsideAllowedTiles(root, false);

    private static bool ContainsViewboxOutsideAllowedTiles(DependencyObject root, bool insideAllowedTile)
    {
        if (root is FrameworkElement { Name: "CustomerIconTile" or "PlateIconTile" or "Weigh1IconTile" or "Weigh2IconTile" })
            insideAllowedTile = true;

        if (root is Viewbox { StretchDirection: StretchDirection.DownOnly })
        {
            // rc12 print-safe auto-fit viewboxes are allowed outside icon tiles.
        }
        else if (root is Viewbox && !insideAllowedTile)
            return true;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child
                && ContainsViewboxOutsideAllowedTiles(child, insideAllowedTile))
                return true;
        }

        return false;
    }

    private static bool ContainsScaleTransform(DependencyObject root)
    {
        if (root is FrameworkElement fe && fe.LayoutTransform is ScaleTransform)
            return true;
        if (root is FrameworkElement fe2 && fe2.RenderTransform is ScaleTransform)
            return true;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child && ContainsScaleTransform(child))
                return true;
        }
        return false;
    }

    private static double GetSafeContentMaxRight(WeighTicketCopyView copy)
    {
        var safeHost = (FrameworkElement)copy.FindName("SafeContentHost")!;
        return GetMaxRightEdge(safeHost, copy);
    }

    private static double GetMaxRightEdge(Visual root, Visual relativeTo)
    {
        var bounds = VisualTreeHelper.GetDescendantBounds(root);
        if (bounds.IsEmpty)
            return 0;

        var transformed = root.TransformToAncestor(relativeTo).TransformBounds(bounds);
        return transformed.Right;
    }
}
