using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;
using CanXe.Desktop.Tests.Support;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc9TicketVisualTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc9TicketVisualTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void HeroCards_ThreeEqualWidthCards()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var cards = FindNamedBorders(view, "HeroCardGross", "HeroCardTare", "HeroCardNet").ToList();
            Assert.Equal(3, cards.Count);
            Assert.InRange(Math.Abs(cards[0].ActualWidth - cards[1].ActualWidth), 0, 1.5);
            Assert.InRange(Math.Abs(cards[1].ActualWidth - cards[2].ActualWidth), 0, 1.5);
        });
    }

    [Fact]
    public void HeroWeightFont_MeetsMinimum29Pt()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var weight = FindFirstTextBlock(view, "11.730");
            Assert.NotNull(weight);
            Assert.True(weight!.FontSize >= WeighTicketPrintTypography.HeroWeightValueDip - 0.5);
            Assert.NotNull(FindFirstTextBlock(view, "kg"));
        });
    }

    [Fact]
    public void CustomerCard_DoesNotContainLabelText()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var card = FindNamedBorder(view, "CustomerIdentityCard");
            Assert.NotNull(card);
            Assert.Null(FindTextInSubtree(card!, "KHÁCH HÀNG"));
        });
    }

    [Fact]
    public void PlateCard_DoesNotContainLabelText()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var card = FindNamedBorder(view, "PlateIdentityCard");
            Assert.NotNull(card);
            Assert.Null(FindTextInSubtree(card!, "BIỂN SỐ XE"));
        });
    }

    [Fact]
    public void IdentityCards_DisplayAndCenterValues()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var customer = FindFirstTextBlock(view, "Cân Dịch Vụ");
            var plate = FindFirstTextBlock(view, "82C08112");
            Assert.NotNull(customer);
            Assert.NotNull(plate);
            Assert.Equal(TextAlignment.Center, customer!.TextAlignment);
            Assert.Equal(TextAlignment.Center, plate!.TextAlignment);
            Assert.Equal(VerticalAlignment.Center, customer.VerticalAlignment);
            Assert.Equal(VerticalAlignment.Center, plate.VerticalAlignment);
            Assert.True(customer.FontSize >= WeighTicketPrintTypography.CustomerValueDip - 0.5);
            Assert.True(plate.FontSize >= WeighTicketPrintTypography.PlateValueDip - 0.5);
        });
    }

    [Fact]
    public void IdentityCards_HaveEqualHeightAnd56By44WidthRatio()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var customer = FindNamedBorder(view, "CustomerIdentityCard");
            var plate = FindNamedBorder(view, "PlateIdentityCard");
            Assert.NotNull(customer);
            Assert.NotNull(plate);
            Assert.Equal(customer!.ActualHeight, plate!.ActualHeight, 1);
            var ratio = customer.ActualWidth / plate.ActualWidth;
            Assert.InRange(ratio, 56.0 / 44.0 - 0.12, 56.0 / 44.0 + 0.12);
        });
    }

    [Fact]
    public void DetailsTable_DoesNotRepeatIdentityOrWeightRows()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.Null(FindSiblingValueAfterLabel(view, "Khách hàng:"));
            Assert.Null(FindSiblingValueAfterLabel(view, "Biển số xe:"));
            Assert.Null(FindSiblingValueAfterLabel(view, "Khối lượng xe + hàng:"));
            Assert.Null(FindSiblingValueAfterLabel(view, "Khối lượng xe:"));
            Assert.Null(FindSiblingValueAfterLabel(view, "Khối lượng hàng:"));
        });
    }

    [Fact]
    public void LongCustomer_DoesNotBreakLayout()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildLongCustomerModel());
            view.UpdateLayout();
            var card = FindNamedBorder(view, "CustomerIdentityCard");
            Assert.NotNull(card);
            Assert.True(card!.ActualHeight <= WeighTicketPrintLayout.IdentityCardsHeightDip + 0.5);
            Assert.Null(FindTextInSubtree(card, "KHÁCH HÀNG"));
        });
    }

    [Fact]
    public void LongPlate_DisplaysWithoutLayoutBreak()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildLongPlateModel());
            view.UpdateLayout();
            var plate = FindFirstTextBlock(view, "81C-123.456");
            Assert.NotNull(plate);
            Assert.True(plate!.ActualWidth > 0);
        });
    }

    [Fact]
    public void WeighBlocks_AreCenterAligned()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.Equal(TextAlignment.Center, FindFirstTextBlock(view, "CÂN LẦN 1")!.TextAlignment);
            Assert.Equal(TextAlignment.Center, FindFirstTextBlock(view, "23:58:10")!.TextAlignment);
            Assert.Equal(TextAlignment.Center, FindFirstTextBlock(view, "29/06/2026")!.TextAlignment);
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
                Assert.True(maxRight <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 0.5);
            }
        });
    }

    [Fact]
    public void TwoCopies_AreIdenticalWithoutCopyLabels()
    {
        _fixture.Invoke(_ =>
        {
            var document = WpfWeighTicketDocumentBuilder.BuildFixedDocument(BuildSampleModel());
            var page = document.DocumentPaginator.GetPage(0);
            var visual = (UIElement)page.Visual;
            var copies = FindChildren<WeighTicketCopyView>(visual).ToList();
            Assert.Equal(2, copies.Count);
            Assert.Null(FindFirstTextBlock(visual, "LIÊN TRẠM CÂN"));
            Assert.Null(FindFirstTextBlock(visual, "CẮT THEO ĐƯỜNG NÉT ĐỨT"));
        });
    }

    [Fact]
    public void Layout_HasNoViewboxOutsideIconTiles()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.False(ContainsViewboxOutsideIconTiles(view));
            Assert.False(ContainsScaleTransformExceptWatermark(view));
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
                Address = "Thôn 7 Xã Ngọc Wang",
                Phone = "0363261945",
                SignLocationName = "Kon Tum"
            },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });

    private static WeighTicketPrintModel BuildLongCustomerModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "1/06",
                CustomerName = "CÔNG TY TNHH THƯƠNG MẠI DỊCH VỤ TIẾN TRỌNG",
                LicensePlate = "81C1234356",
                TicketDateTime = DateTimeOffset.Now
            },
            new StationSettingsDto { StationName = "Trạm" },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });

    private static WeighTicketPrintModel BuildLongPlateModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "1/06",
                CustomerName = "Khách",
                LicensePlate = "81C-123.456",
                TicketDateTime = DateTimeOffset.Now
            },
            new StationSettingsDto { StationName = "Trạm" },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });

    private static WeighTicketPrintModel BuildStressModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "999999/12/NK",
                CustomerName = "CÔNG TY TNHH THƯƠNG MẠI DỊCH VỤ TIẾN TRỌNG",
                LicensePlate = "81C-123.456",
                CargoTypeName = "Rơ tươi",
                Weight1Kg = 99_999m,
                Weight2Kg = 89_999m,
                GrossWeightKg = 99_999m,
                TareWeightKg = 89_999m,
                NetWeightKg = 10_000m,
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
                SignLocationName = "Kon Tum"
            },
            new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });

    private static IEnumerable<Border> FindNamedBorders(DependencyObject root, params string[] names) =>
        names.Select(n => FindNamedBorder(root, n)).Where(b => b is not null).Cast<Border>();

    private static Border? FindNamedBorder(DependencyObject root, string name)
    {
        if (root is FrameworkElement fe && fe.Name == name && root is Border border)
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

    private static TextBlock? FindSiblingValueAfterLabel(DependencyObject root, string labelText)
    {
        if (root is Grid grid)
        {
            TextBlock? label = null;
            TextBlock? value = null;
            foreach (var child in grid.Children)
            {
                if (child is TextBlock tb)
                {
                    if (tb.Text == labelText)
                        label = tb;
                    else if (Grid.GetColumn(tb) == 1)
                        value = tb;
                }
            }
            if (label is not null && value is not null)
                return value;
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindSiblingValueAfterLabel(child, labelText);
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

    private static bool ContainsScaleTransformExceptWatermark(DependencyObject root)
    {
        if (root is TextBlock { Text: "BẢN IN LẠI" })
            return false;
        if (root is FrameworkElement fe && (fe.LayoutTransform is ScaleTransform || fe.RenderTransform is ScaleTransform))
            return true;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child && ContainsScaleTransformExceptWatermark(child))
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
        return root.TransformToAncestor(relativeTo).TransformBounds(bounds).Right;
    }
}
