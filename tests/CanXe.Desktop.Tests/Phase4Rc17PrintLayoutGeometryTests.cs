using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc17PrintLayoutGeometryTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc17PrintLayoutGeometryTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void CopyLayout_PassesGeometryGateWithZeroIntersectionsAndTouches()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel());
            var report = PrintLayoutGeometryValidator.ValidateMaterializedCopy(copy);
            Assert.True(report.Passed, string.Join(Environment.NewLine, report.Lines));
            Assert.Equal(0, report.IntersectionCount);
            Assert.Equal(0, report.TouchCount);
            var gapToleranceMm = WeighTicketPrintLayout.DipToMm(WeighTicketPrintLayout.GeometryToleranceDip);
            Assert.True(report.HeroToBodyGapMm >= WeighTicketPrintLayout.HeroBodyGapMm - gapToleranceMm);
            Assert.True(report.BodySignatureGapMm >= WeighTicketPrintLayout.BodySignatureGapMm - gapToleranceMm);
        });
    }

    [Fact]
    public void AllCards_UseCardHostWithSharedInset()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel());
            foreach (var name in new[]
                     {
                         "HeroCardGross", "HeroCardTare", "HeroCardNet",
                         "CustomerIdentityCard", "PlateIdentityCard",
                         "DetailsTableCard", "TimestampCard"
                     })
            {
                var card = FindNamed<Border>(copy, name);
                Assert.NotNull(card);
                var host = VisualTreeHelper.GetParent(card!) as Border;
                Assert.NotNull(host);
                Assert.False(host!.ClipToBounds);
                Assert.Equal(0, host.BorderThickness.Left);
                Assert.Equal(WeighTicketPrintLayout.CardHostInsetDip, host.Padding.Left, 1);
            }
        });
    }

    [Fact]
    public void IdentityCards_HaveFourEdgesAndNoClipToBounds()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel());
            var customer = FindNamed<Border>(copy, "CustomerIdentityCard");
            var vehicle = FindNamed<Border>(copy, "PlateIdentityCard");
            Assert.NotNull(customer);
            Assert.NotNull(vehicle);
            Assert.False(customer!.ClipToBounds);
            Assert.False(vehicle!.ClipToBounds);
            Assert.Equal(0.75, customer.BorderThickness.Left, 2);
            Assert.Equal(0.75, vehicle.BorderThickness.Left, 2);
        });
    }

    [Fact]
    public void TopAndBottomCopies_BothPassGeometryGate()
    {
        _fixture.Invoke(_ =>
        {
            var doc = new WpfWeighTicketDocumentFactory().CreateA4TwoUpDocument(BuildSampleModel());
            var page = doc.Pages[0].GetPageRoot(false) as FixedPage;
            var copies = page!.Children.OfType<WeighTicketCopyView>().OrderBy(c => FixedPage.GetTop(c)).ToList();
            var top = copies[0];
            var bottom = copies[1];
            var topReport = PrintLayoutGeometryValidator.ValidateMaterializedCopy(top);
            var bottomReport = PrintLayoutGeometryValidator.ValidateMaterializedCopy(bottom);
            Assert.True(topReport.Passed, string.Join(Environment.NewLine, topReport.Lines));
            Assert.True(bottomReport.Passed, string.Join(Environment.NewLine, bottomReport.Lines));
            Assert.Equal(topReport.HeroToBodyGapMm, bottomReport.HeroToBodyGapMm, 1);
        });
    }

    [Fact]
    public void ClippingStressCases_DoNotOverflowCards()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildStressModel());
            WpfWeighTicketDocumentFactory.Materialize(copy);

            var customer = FindNamed<Border>(copy, "CustomerIdentityCard");
            var plate = FindNamed<Border>(copy, "PlateIdentityCard");
            var timestamp = FindNamed<Border>(copy, "TimestampCard");
            AssertTextWithinCard(copy, customer!, FindNamed<TextBlock>(copy, "CustomerValueText")!);
            AssertTextWithinCard(copy, plate!, FindNamed<TextBlock>(copy, "PlateValueText")!);
            AssertTextWithinCard(copy, timestamp!, FindNamed<TextBlock>(copy, "Weigh1TimeText")!);
        });
    }

    private static void AssertTextWithinCard(Visual root, Border card, TextBlock text)
    {
        var cardBounds = GetBounds(root, card);
        var textBounds = GetBounds(root, text);
        Assert.True(textBounds.Left >= cardBounds.Left - 0.5);
        Assert.True(textBounds.Right <= cardBounds.Right + 0.5);
        Assert.True(textBounds.Top >= cardBounds.Top - 0.5);
        Assert.True(textBounds.Bottom <= cardBounds.Bottom + 0.5);
    }

    private static Rect GetBounds(Visual root, FrameworkElement element)
    {
        var transform = element.TransformToAncestor(root);
        var bounds = VisualTreeHelper.GetDescendantBounds(element);
        if (bounds.IsEmpty)
            bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);

        var topLeft = transform.Transform(bounds.TopLeft);
        var bottomRight = transform.Transform(new Point(bounds.Right, bounds.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private static WeighTicketPrintModel BuildSampleModel() => BuildModel(
        "Khách Hàng C",
        "81C1234356",
        "15/06",
        new DateTimeOffset(2026, 6, 29, 12, 45, 33, TimeSpan.FromHours(7)),
        new DateTimeOffset(2026, 6, 29, 12, 46, 7, TimeSpan.FromHours(7)));

    private static WeighTicketPrintModel BuildStressModel() => BuildModel(
        "CÔNG TY TNHH THƯƠNG MẠI DỊCH VỤ TIẾN TRỌNG",
        "81C-123.456",
        "999999/12/NK",
        new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(7)),
        new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(7)));

    private static WeighTicketPrintModel BuildModel(
        string customer,
        string plate,
        string displayNumber,
        DateTimeOffset w1,
        DateTimeOffset w2) =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = displayNumber,
                CustomerName = customer,
                LicensePlate = plate,
                CargoTypeName = "Rổ nhãn",
                GrossWeightKg = 180m,
                TareWeightKg = 170m,
                NetWeightKg = 10m,
                UnitPriceVndPerKg = 80_000m,
                TotalAmountVnd = 800_000m,
                Weight1RecordedAt = w1,
                Weight2RecordedAt = w2,
                TicketDateTime = w2
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
                Phone = "0865407403"
            },
            new PrintSettingsDto { PrintRenderingMode = PrintRenderingMode.RasterCompatibility },
            isReprint: false);

    private static T? FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        if (root is T fe && fe.Name == name)
            return fe;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
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
}
