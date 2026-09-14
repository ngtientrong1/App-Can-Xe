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

/// <summary>
/// rc24: the printed ticket was redesigned around stacked horizontal "bands" (hero, weigh, info,
/// notes/total) approved via the CanXe · Phiếu cân v3 mockup, replacing the old three-card hero,
/// paired identity cards, and separate details/timestamp tables. These tests cover the new
/// structure; the superseded card-geometry tests (rc14-17, rc19 identity icons) were removed
/// since their entire premise — named side-by-side cards and icon tiles — no longer exists.
/// </summary>
[Collection("WpfSta")]
public sealed class Phase4Rc24TicketBandRedesignTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc24TicketBandRedesignTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void HeroBand_ShowsPlateAndNetWeightTogether()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.NotNull(FindFirstTextBlock(view, "BIỂN SỐ XE"));
            Assert.NotNull(FindFirstTextBlock(view, "KHỐI LƯỢNG HÀNG"));
            Assert.NotNull(FindFirstTextBlock(view, "82C08112"));
            Assert.NotNull(FindFirstTextBlock(view, "6.750"));
        });
    }

    [Fact]
    public void WeighBand_ShowsGrossAndTareWithTimestamps()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.NotNull(FindFirstTextBlock(view, "CÂN LẦN 1"));
            Assert.NotNull(FindFirstTextBlock(view, "CÂN LẦN 2"));
            Assert.NotNull(FindFirstTextBlock(view, "11.730"));
            Assert.NotNull(FindFirstTextBlock(view, "4.980"));
            Assert.NotNull(FindFirstTextBlock(view, "23:58:10"));
            Assert.NotNull(FindFirstTextBlock(view, "30/06/2026"));
        });
    }

    [Fact]
    public void WeighBand_ShowsChuaCanWhenAWeighingIsMissing()
    {
        _fixture.Invoke(_ =>
        {
            var model = WeighTicketPrintModelMapper.FromDetail(
                new WeighTicketDetailDto
                {
                    Id = 1,
                    DisplayNumber = "1/06",
                    LicensePlate = "81C1234356",
                    TicketDateTime = DateTimeOffset.Now
                },
                new StationSettingsDto { StationName = "Trạm" },
                new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });
            var view = ArrangeView(model);
            Assert.Equal(2, CountTextBlocks(view, "CHƯA CÂN"));
        });
    }

    [Fact]
    public void InfoBand_ShowsCustomerCargoPriceAndDeduction()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.NotNull(FindFirstTextBlock(view, "KHÁCH HÀNG"));
            Assert.NotNull(FindFirstTextBlock(view, "LOẠI HÀNG"));
            Assert.NotNull(FindFirstTextBlock(view, "ĐƠN GIÁ"));
            Assert.NotNull(FindFirstTextBlock(view, "TRỪ HAO"));
            Assert.NotNull(FindFirstTextBlock(view, "Cân Dịch Vụ"));
            Assert.NotNull(FindFirstTextBlock(view, "Rơ tươi"));

            var deduction = FindNamedTextBlock(view, "DeductionWeightValueText");
            Assert.NotNull(deduction);
            Assert.Equal("9 kg", deduction!.Text);
        });
    }

    [Fact]
    public void InfoBand_ShowsDashWhenDeductionNotSet()
    {
        _fixture.Invoke(_ =>
        {
            var model = WeighTicketPrintModelMapper.FromDetail(
                new WeighTicketDetailDto
                {
                    Id = 1,
                    DisplayNumber = "1/06",
                    TicketDateTime = DateTimeOffset.Now
                },
                new StationSettingsDto { StationName = "Trạm" },
                new PrintSettingsDto { PrintLayoutMode = PrintLayoutMode.A4TwoUp });
            var view = ArrangeView(model);
            var deduction = FindNamedTextBlock(view, "DeductionWeightValueText");
            Assert.NotNull(deduction);
            Assert.Equal("—", deduction!.Text);
        });
    }

    [Fact]
    public void BottomRow_ShowsNotesAndHighlightedTotalCard()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.NotNull(FindFirstTextBlock(view, "GHI CHÚ"));
            Assert.NotNull(FindFirstTextBlock(view, "Giao buổi chiều"));
            Assert.NotNull(FindFirstTextBlock(view, "THÀNH TIỀN"));

            var totalValue = FindNamedTextBlock(view, "TotalAmountValueText");
            Assert.NotNull(totalValue);
            var card = FindAncestor<Border>(totalValue!);
            Assert.NotNull(card);
            Assert.True(card!.BorderThickness.Left > 0);
            Assert.NotEqual(Colors.Transparent, ((SolidColorBrush)card.Background).Color);

            // The total is a contained card, not a page-wide banner.
            Assert.True(card.ActualWidth < WeighTicketPrintLayout.ContentUsableWidthDip * 0.7);
        });
    }

    [Fact]
    public void Signature_HasNoUnderlineOrHintCaption()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildSampleModel());
            Assert.NotNull(FindFirstTextBlock(view, "LÁI XE"));
            Assert.NotNull(FindFirstTextBlock(view, "CHỦ HÀNG"));
            Assert.NotNull(FindFirstTextBlock(view, "NGƯỜI CÂN"));
            Assert.Null(FindFirstTextBlock(view, "Ký, ghi rõ họ tên"));
            Assert.Null(FindFirstTextBlock(view, "(Ký và ghi rõ họ tên)"));
        });
    }

    [Fact]
    public void CopyContent_StaysWithinSafeArea()
    {
        _fixture.Invoke(_ =>
        {
            var view = ArrangeView(BuildStressModel());
            view.UpdateLayout();
            var safeHost = (FrameworkElement)view.FindName("SafeContentHost")!;
            var bounds = VisualTreeHelper.GetDescendantBounds(safeHost);
            var maxRight = safeHost.TransformToAncestor(view).TransformBounds(bounds).Right;
            Assert.True(maxRight <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 1.5,
                $"Right edge {maxRight} exceeded safe edge {WeighTicketPrintLayout.CopySafeRightEdgeDip}");
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
                DeductionWeightKg = 9m,
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
                CustomerName = "CÔNG TY TNHH THƯƠNG MẠI DỊCH VỤ TIẾN TRỌNG",
                LicensePlate = "81C-123.456",
                CargoTypeName = "Rơ tươi",
                Notes = "Giao buổi chiều, hàng ẩm nhẹ, đã kiểm tra kỹ trước khi xuất bến",
                Weight1Kg = 99_999m,
                Weight2Kg = 89_999m,
                GrossWeightKg = 99_999m,
                TareWeightKg = 89_999m,
                NetWeightKg = 10_000m,
                DeductionWeightKg = 99m,
                UnitPriceVndPerKg = 89_000m,
                TotalAmountVnd = 999_999_999m,
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

    private static int CountTextBlocks(DependencyObject root, string text)
    {
        var count = 0;
        if (root is TextBlock tb && tb.Text == text && tb.Visibility == Visibility.Visible)
            count++;
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
                count += CountTextBlocks(child, text);
        }
        return count;
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
}
