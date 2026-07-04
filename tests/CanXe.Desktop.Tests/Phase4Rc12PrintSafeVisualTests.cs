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
public sealed class Phase4Rc12PrintSafeVisualTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc12PrintSafeVisualTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void IdentityValues_UseAutoFitWithoutEllipsis()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel());
            var customer = FindNamed<TextBlock>(copy, "CustomerValueText");
            var plate = FindNamed<TextBlock>(copy, "PlateValueText");
            Assert.NotNull(customer);
            Assert.NotNull(plate);
            Assert.Equal("Cân Dịch Vụ", customer!.Text);
            Assert.Equal("51A-12345", plate!.Text);
            Assert.NotEqual(TextTrimming.CharacterEllipsis, customer.TextTrimming);
            Assert.NotEqual(TextTrimming.CharacterEllipsis, plate.TextTrimming);
            Assert.NotNull(FindAncestor<Viewbox>(customer));
            Assert.NotNull(FindAncestor<Viewbox>(plate));
        });
    }

    [Fact]
    public void Content_StaysWithinSafeRightEdge()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel());
            var safeHost = (FrameworkElement)copy.FindName("SafeContentHost")!;
            var maxRight = GetMaxRightEdge(safeHost, copy);
            Assert.True(maxRight <= WeighTicketPrintLayout.CopySafeRightEdgeDip + 1.0,
                $"Right edge {maxRight} exceeded safe {WeighTicketPrintLayout.CopySafeRightEdgeDip}");
        });
    }

    [Fact]
    public void RasterSafetyInset_FormulasMatchRc13()
    {
        var insetH = WeighTicketPrintLayout.PrinterImageableSafetyInsetHorizontalDip;
        var insetV = WeighTicketPrintLayout.PrinterImageableSafetyInsetVerticalDip;
        Assert.Equal(WeighTicketPrintLayout.MmToDip(3.0), insetH, 1);
        Assert.Equal(WeighTicketPrintLayout.MmToDip(2.5), insetV, 1);

        const double ox = 37.8;
        const double oy = 37.8;
        const double ew = 718.0;
        const double eh = 1047.0;
        var targetLeft = ox + insetH;
        var targetTop = oy + insetV;
        var targetWidth = ew - insetH * 2;
        var targetHeight = eh - insetV * 2;
        var scale = Math.Min(
            targetWidth / WeighTicketPrintLayout.PageWidthDip,
            targetHeight / WeighTicketPrintLayout.PageHeightDip);
        Assert.True(scale > 0 && scale < 1.05);
        Assert.True(targetLeft + targetWidth <= ox + ew + 0.5);
    }

    private static WeighTicketPrintModel BuildSampleModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "06/123",
                CustomerName = "Cân Dịch Vụ",
                LicensePlate = "51A-12345",
                CargoTypeName = "Rổ nhãn",
                GrossWeightKg = 180m,
                TareWeightKg = 170m,
                NetWeightKg = 10m,
                UnitPriceVndPerKg = 80_000m,
                TotalAmountVnd = 800_000m,
                Weight1RecordedAt = new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero),
                Weight2RecordedAt = new DateTimeOffset(2026, 6, 30, 11, 0, 0, TimeSpan.Zero),
                TicketDateTime = new DateTimeOffset(2026, 6, 30, 11, 0, 0, TimeSpan.Zero)
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN"
            },
            new PrintSettingsDto { PrintRenderingMode = PrintRenderingMode.RasterCompatibility });

    private static double GetMaxRightEdge(Visual root, Visual relativeTo)
    {
        var bounds = VisualTreeHelper.GetDescendantBounds(root);
        if (bounds.IsEmpty)
            return 0;

        return root.TransformToAncestor(relativeTo).TransformBounds(bounds).Right;
    }

    private static Rect GetVisualBounds(FrameworkElement element)
    {
        element.UpdateLayout();
        var topLeft = element.TransformToVisual(element).Transform(new Point(0, 0));
        return new Rect(topLeft.X, topLeft.Y, element.ActualWidth, element.ActualHeight);
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

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T match)
                return match;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
