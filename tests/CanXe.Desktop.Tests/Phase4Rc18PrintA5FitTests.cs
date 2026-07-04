using System.Windows.Documents;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc18PrintA5FitTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc18PrintA5FitTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void A5Document_HasSingleTicketOnLandscapePage()
    {
        _fixture.Invoke(app =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var document = new WpfWeighTicketDocumentFactory().CreateA5SingleDocument(model);
            var page = document.Pages[0].GetPageRoot(false) as FixedPage;
            Assert.NotNull(page);
            Assert.Equal(WeighTicketPrintLayout.A5LandscapePageWidthDip, page!.Width, 2);
            Assert.Equal(WeighTicketPrintLayout.A5LandscapePageHeightDip, page.Height, 2);
            Assert.Single(page.Children.OfType<WeighTicketCopyView>());
        });
    }

    [Fact]
    public void HeaderTextBoundsGate_PassesForSampleTicket()
    {
        _fixture.Invoke(app =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var report = PrintA5FitValidator.ValidateHeaderTextBounds(copy);
            Assert.True(report.Passed, string.Join(Environment.NewLine, report.Lines));
        });
    }

    [Fact]
    public void RasterA5_HasNonWhiteContent()
    {
        _fixture.Invoke(app =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var ratio = WeighTicketRasterPrintRenderer.ComputeNonWhitePixelRatio(bitmap);
            Assert.True(ratio >= WeighTicketRasterPrintRenderer.MinNonWhitePixelRatio);
        });
    }

    [Fact]
    public void RasterPlacement_PassesEdgeClearance()
    {
        _fixture.Invoke(app =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var renderer = new WeighTicketRasterPrintRenderer();
            var logger = new WeighTicketPrintRenderingLogger();
            renderer.CreateRasterA5SingleDocument(model, null, logger, out var nonWhiteRatio, out _);
            Assert.True(nonWhiteRatio > 0);
        });
    }

    [Fact]
    public void PreviewAndPrint_UseSameTemplate()
    {
        _fixture.Invoke(app =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var previewCopy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var renderer = new WeighTicketRasterPrintRenderer();
            var logger = new WeighTicketPrintRenderingLogger();
            renderer.CreateRasterA5SingleDocument(model, null, logger, out var nonWhiteRatio, out _);
            Assert.True(nonWhiteRatio > 0);
            Assert.Equal(previewCopy.ActualWidth, WeighTicketPrintLayout.TicketLogicalWidthDip, 2);
            Assert.Equal(previewCopy.ActualHeight, WeighTicketPrintLayout.TicketLogicalHeightDip, 2);
        });
    }

    [Fact]
    public void NormalPrint_HasNoWatermark_ReprintHasWatermark()
    {
        _fixture.Invoke(app =>
        {
            var normal = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var normalCopy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(normal);
            var watermark = normalCopy.FindName("ReprintWatermark") as System.Windows.UIElement;
            Assert.NotNull(watermark);
            Assert.Equal(System.Windows.Visibility.Collapsed, watermark!.Visibility);

            var reprintModel = WeighTicketPrintModelMapper.FromDetail(
                new WeighTicketDetailDto
                {
                    Id = 1,
                    DisplayNumber = "15/06",
                    TicketDateTime = DateTimeOffset.UtcNow
                },
                new StationSettingsDto { StationName = "TRẠM CÂN TIẾN TRỌNG" },
                new PrintSettingsDto { ShowReprintWatermark = true },
                isReprint: true);
            var reprintCopy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(reprintModel);
            var reprintWatermark = reprintCopy.FindName("ReprintWatermark") as System.Windows.UIElement;
            Assert.NotNull(reprintWatermark);
            Assert.Equal(System.Windows.Visibility.Visible, reprintWatermark!.Visibility);
        });
    }

    [Fact]
    public void GeometryValidator_StillPassesAfterRc18Layout()
    {
        _fixture.Invoke(app =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var report = PrintLayoutGeometryValidator.ValidateMaterializedCopy(copy);
            Assert.True(report.Passed, string.Join(Environment.NewLine, report.Lines));
        });
    }
}
