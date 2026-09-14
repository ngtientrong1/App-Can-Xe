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
public sealed class Phase4Rc7TicketVisualTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc7TicketVisualTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void CopyRoot_HasNoOuterBorder()
    {
        _fixture.Invoke(_ =>
        {
            var document = WpfWeighTicketDocumentBuilder.BuildFixedDocument(BuildSampleModel());
            var page = document.DocumentPaginator.GetPage(0);
            var visual = (UIElement)page.Visual;
            Assert.False(ContainsOuterCopyBorder(visual));
        });
    }

    [Fact]
    public void CopyContent_DoesNotExceedSafeRightEdge()
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
                Assert.True(maxRight <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 1.5,
                    $"Copy content right edge {maxRight} exceeded safe edge {WeighTicketPrintLayout.CopySafeRightEdgeDip}");
            }
        });
    }

    [Fact]
    public void CustomerValue_IsInIdentityCard()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.Null(FindSiblingValueAfterLabel(view, "Khách hàng:"));
            Assert.NotNull(FindFirstTextBlock(view, "Cân Dịch Vụ"));
        });
    }

    [Fact]
    public void PlateAndCargoValues_AreDisplayed()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.NotNull(FindFirstTextBlock(view, "82C08112"));
            Assert.NotNull(FindFirstTextBlock(view, "Rơ tươi"));
        });
    }

    [Fact]
    public void NetWeight_IsInHeroBand()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.Null(FindSiblingValueAfterLabel(view, "Khối lượng xe + hàng:"));
            Assert.NotNull(FindFirstTextBlock(view, "KHỐI LƯỢNG HÀNG"));
        });
    }

    [Fact]
    public void NotesValue_IsDisplayed()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var notes = FindFirstTextBlock(view, "Giao buổi chiều");
            Assert.NotNull(notes);
            Assert.Equal(TextAlignment.Left, notes!.TextAlignment);
        });
    }

    [Fact]
    public void WeighBlocks_TitlesAreLeftAlignedWithValueOnTheRight()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var title1 = FindFirstTextBlock(view, "CÂN LẦN 1");
            var time = FindFirstTextBlock(view, "23:58:10");
            var date = FindFirstTextBlock(view, "29/06/2026");
            var title2 = FindFirstTextBlock(view, "CÂN LẦN 2");
            Assert.NotNull(title1);
            Assert.Equal(TextAlignment.Left, title1!.TextAlignment);
            Assert.NotNull(time);
            Assert.NotNull(date);
            Assert.NotNull(title2);
            Assert.NotNull(FindFirstTextBlock(view, "11.730"));
            Assert.NotNull(FindFirstTextBlock(view, "4.980"));
        });
    }

    [Fact]
    public void SignDate_IsDisplayed()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            var signDate = FindFirstTextBlockContaining(view, "Kon Tum, ngày");
            Assert.NotNull(signDate);
            Assert.Equal(TextAlignment.Right, signDate!.TextAlignment);
        });
    }

  [Fact]
  public void Weights_DisplayIntegerKilograms()
  {
    _fixture.Invoke(_ =>
    {
      var model = WeighTicketPrintModelMapper.FromDetail(
          new WeighTicketDetailDto
          {
            Id = 1,
            DisplayNumber = "24/06",
            GrossWeightKg = 180m,
            TareWeightKg = 170m,
            NetWeightKg = 10m,
            Weight1RecordedAt = DateTimeOffset.Now,
            Weight2RecordedAt = DateTimeOffset.Now,
            TicketDateTime = DateTimeOffset.Now
          },
          new StationSettingsDto { StationName = "Trạm" },
          new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });
      var vm = WeighTicketCopyViewModel.From(model);
      Assert.Equal("180 kg", vm.GrossWeightDisplay);
      Assert.Equal("170 kg", vm.TareWeightDisplay);
      Assert.Equal("10 kg", vm.NetWeightDisplay);
      Assert.DoesNotContain(",000", vm.GrossWeightDisplay, StringComparison.Ordinal);
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

    private static bool ContainsOuterCopyBorder(DependencyObject root)
    {
        if (root is Border border &&
            border.BorderThickness.Left > 0 &&
            border.Child is WeighTicketCopyView)
            return true;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child && ContainsOuterCopyBorder(child))
                return true;
        }

        return false;
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

    private static TextBlock? FindFirstTextBlockContaining(DependencyObject root, string fragment)
    {
        if (root is TextBlock tb && tb.Text.Contains(fragment, StringComparison.Ordinal))
            return tb;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindFirstTextBlockContaining(child, fragment);
                if (found is not null)
                    return found;
            }
        }
        return null;
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
