using System.Windows;
using System.Windows.Controls;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.Views.Printing;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc14PrintLayoutGeometryTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc14PrintLayoutGeometryTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void CopyLayout_HasNoCardIntersections()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel());
            var report = PrintLayoutGeometryValidator.ValidateMaterializedCopy(copy);
            Assert.True(report.Passed, string.Join(Environment.NewLine, report.Lines));
            Assert.Equal(0, report.IntersectionCount);
            Assert.Null(FindNamed<Border>(copy, "OuterFrame"));
        });
    }

    [Fact]
    public void HeroCards_HaveEqualHeightAndThreeMmGaps()
    {
        _fixture.Invoke(_ =>
        {
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(BuildSampleModel());
            var h1 = FindNamed<Border>(copy, "HeroCardGross");
            var h2 = FindNamed<Border>(copy, "HeroCardTare");
            var h3 = FindNamed<Border>(copy, "HeroCardNet");
            Assert.NotNull(h1);
            Assert.NotNull(h2);
            Assert.NotNull(h3);
            Assert.Equal(h1!.ActualHeight, h2!.ActualHeight, 1);
            Assert.Equal(h2.ActualHeight, h3!.ActualHeight, 1);
        });
    }

    private static WeighTicketPrintModel BuildSampleModel() =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
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
                TicketDateTime = new DateTimeOffset(2026, 6, 29, 12, 45, 0, TimeSpan.FromHours(7))
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

        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (System.Windows.Media.VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindNamed<T>(child, name);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }
}
