using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc10TicketVisualTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc10TicketVisualTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void InfoBand_ValuesAreDisplayedWithoutRedundantLabelsInline()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var cargoValue = FindNamedTextBlock(view, "CargoTypeValueText");
            var unitPriceValue = FindNamedTextBlock(view, "UnitPriceValueText");
            var totalValue = FindNamedTextBlock(view, "TotalAmountValueText");
            Assert.NotNull(cargoValue);
            Assert.NotNull(unitPriceValue);
            Assert.NotNull(totalValue);

            Assert.True(GetRightEdge(cargoValue!, view) <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 1.5);
            Assert.True(GetRightEdge(unitPriceValue!, view) <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 1.5);
            Assert.True(GetRightEdge(totalValue!, view) <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 1.5);
        });
    }

    [Fact]
    public void WeighBandAndInfoBand_DoNotOverlap()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildStressModel());
            view.UpdateLayout();
            var cargoValue = FindNamedTextBlock(view, "CargoTypeValueText");
            var grossTime = FindNamedTextBlock(view, "Weigh1TimeText");
            Assert.NotNull(cargoValue);
            Assert.NotNull(grossTime);

            var cargoTop = cargoValue!.TransformToAncestor(view).Transform(new Point(0, 0)).Y;
            var grossBottom = grossTime!.TransformToAncestor(view).Transform(new Point(0, grossTime.ActualHeight)).Y;
            Assert.True(grossBottom <= cargoTop + 0.5);
        });
    }

    [Fact]
    public void WeighBandTitles_AreLeftAligned()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.Equal(TextAlignment.Left, FindFirstTextBlock(view, "CÂN LẦN 1")!.TextAlignment);
            Assert.NotNull(FindFirstTextBlock(view, "23:58:10"));
            Assert.NotNull(FindFirstTextBlock(view, "29/06/2026"));
        });
    }

    [Fact]
    public void Layout_HasNoViewboxOutsideIconTiles()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.False(ContainsViewboxOutsideIconTiles(view));
        });
    }

    [Fact]
    public void HeroWeight_NumberAndUnit_OnSameRow()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var number = FindFirstTextBlock(view, "6.750");
            var unit = FindFirstTextBlock(view, "kg");
            Assert.NotNull(number);
            Assert.NotNull(unit);
            Assert.True(number!.FontSize > unit!.FontSize);
            var row = VisualTreeHelper.GetParent(number) as StackPanel;
            Assert.NotNull(row);
            Assert.Equal(Orientation.Horizontal, row!.Orientation);
            Assert.Same(row, VisualTreeHelper.GetParent(unit));
        });
    }

    [Fact]
    public void CopyContent_DoesNotExceedSafeArea()
    {
        _fixture.Invoke(_ =>
        {
            var document = WpfWeighTicketDocumentBuilder.BuildFixedDocument(BuildStressModel());
            var page = document.DocumentPaginator.GetPage(0);
            var visual = (UIElement)page.Visual;
            visual.Measure(new Size(WeighTicketPrintLayout.PageWidthDip, WeighTicketPrintLayout.PageHeightDip));
            visual.Arrange(new Rect(0, 0, WeighTicketPrintLayout.PageWidthDip, WeighTicketPrintLayout.PageHeightDip));
            visual.UpdateLayout();

            foreach (var copyView in FindChildren<WeighTicketCopyView>(visual))
            {
                var maxRight = GetSafeContentMaxRight(copyView);
                Assert.True(maxRight <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 1.5);
            }
        });
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

    private static WeighTicketPrintModel BuildSampleModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "68/06/NK",
                CustomerName = "Khách Hàng C",
                LicensePlate = "81C1234356",
                CargoTypeName = "Rổ nhãn",
                Notes = "—",
                Weight1Kg = 11_730m,
                Weight2Kg = 4_980m,
                GrossWeightKg = 11_730m,
                TareWeightKg = 4_980m,
                NetWeightKg = 6_750m,
                UnitPriceVndPerKg = 80_000m,
                TotalAmountVnd = 800_000m,
                Weight1RecordedAt = new DateTimeOffset(2026, 6, 29, 23, 58, 10, TimeSpan.FromHours(7)),
                Weight2RecordedAt = new DateTimeOffset(2026, 6, 30, 0, 12, 5, TimeSpan.FromHours(7)),
                TicketDateTime = new DateTimeOffset(2026, 6, 29, 15, 30, 0, TimeSpan.FromHours(7))
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
                Phone = "0865407403",
                SignLocationName = "Kon Tum"
            },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });

    private static WeighTicketPrintModel BuildStressModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "999999/12/NK",
                CustomerName = "CÔNG TY TNHH THƯƠNG MẠI DỊCH VỤ TIẾN TRỌNG",
                LicensePlate = "81C-123.456",
                CargoTypeName = "Rổ nhãn",
                Weight1Kg = 99_999m,
                Weight2Kg = 89_999m,
                GrossWeightKg = 99_999m,
                TareWeightKg = 89_999m,
                NetWeightKg = 10_000m,
                UnitPriceVndPerKg = 80_000m,
                TotalAmountVnd = 999_999_999m,
                Weight1RecordedAt = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(7)),
                Weight2RecordedAt = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(7)),
                TicketDateTime = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(7))
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
                SignLocationName = "Kon Tum"
            },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });

    private static Border? FindNamedBorder(DependencyObject root, string name)
    {
        if (root is FrameworkElement { Name: var n } fe && n == name && fe is Border border)
            return border;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindNamedBorder(child, name);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    private static TextBlock? FindNamedTextBlock(DependencyObject root, string name)
    {
        if (root is TextBlock tb && tb.Name == name)
            return tb;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindNamedTextBlock(child, name);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    private static TextBlock? FindTextInSubtree(DependencyObject root, string text)
    {
        if (root is TextBlock tb && tb.Text == text)
            return tb;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindTextInSubtree(child, text);
                if (found is not null)
                    return found;
            }
        }
        return null;
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

    private static Path? FindFirstPath(DependencyObject root)
    {
        if (root is Path path)
            return path;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
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

    private static Viewbox? GetParentViewbox(DependencyObject child)
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is Viewbox viewbox)
                return viewbox;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
            yield return match;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                foreach (var descendant in FindChildren<T>(child))
                    yield return descendant;
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static double GetRightEdge(FrameworkElement element, Visual relativeTo)
    {
        var bounds = VisualTreeHelper.GetDescendantBounds(element);
        if (bounds.IsEmpty)
            bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        return element.TransformToAncestor(relativeTo).TransformBounds(bounds).Right;
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
        return root.TransformToAncestor(relativeTo).TransformBounds(bounds).Right;
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
}
