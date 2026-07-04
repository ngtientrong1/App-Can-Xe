using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc22A5OrientationTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc22A5OrientationTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void RotateClockwise90_SwapsPixelDimensions()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var rotated = A5RasterBitmapRotator.RotateClockwise90(bitmap);

            Assert.Equal(bitmap.PixelHeight, rotated.PixelWidth);
            Assert.Equal(bitmap.PixelWidth, rotated.PixelHeight);
        });
    }

    [Fact]
    public void RasterA5_PortraitPageCapabilities_StillPassesAdaptiveFit()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var landscapeBitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var pageW = WeighTicketPrintLayout.A5LandscapePageHeightDip;
            var pageH = WeighTicketPrintLayout.A5LandscapePageWidthDip;
            var orientation = A5PhysicalOrientationResolver.Resolve(
                pageW,
                pageH,
                PageOrientationLabel.Landscape,
                PageOrientationLabel.Landscape);
            var rotated = A5RasterBitmapRotator.RotateClockwise90(landscapeBitmap);
            var landscapeBounds = RasterContentBoundsAnalyzer.AnalyzeTicketBitmap(landscapeBitmap, scanWatermark: false);
            var rawMainRotated = A5PrintBoundsTransformer.RotateBoundsClockwise90(
                landscapeBounds.MainContentBoundsDip.IsEmpty
                    ? ContentBoundsDip.FromSize(
                        WeighTicketPrintLayout.TicketLogicalWidthDip,
                        WeighTicketPrintLayout.TicketLogicalHeightDip)
                    : landscapeBounds.MainContentBoundsDip,
                WeighTicketPrintLayout.TicketLogicalWidthDip,
                WeighTicketPrintLayout.TicketLogicalHeightDip);
            var adaptive = PrintA5AdaptiveFitService.ResolveWithMainBounds(
                rotated,
                imageableArea: null,
                pageW,
                pageH,
                orientation.RotatedContentWidthDip,
                orientation.RotatedContentHeightDip,
                scanWatermark: false,
                orientation,
                rawMainRotated);

            Assert.True(adaptive.Passed);
            Assert.Equal(A5PhysicalOrientationMode.PortraitDriverFallbackRotateClockwise, orientation.Mode);
        });
    }

    [Fact]
    public void CreateRasterDocument_PortraitCapabilities_UsesActualPageSize()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var pageW = WeighTicketPrintLayout.A5LandscapePageHeightDip;
            var pageH = WeighTicketPrintLayout.A5LandscapePageWidthDip;
            var orientation = A5PhysicalOrientationResolver.Resolve(
                pageW,
                pageH,
                PageOrientationLabel.Landscape,
                PageOrientationLabel.Portrait);
            var caps = new PrinterCapabilitiesReader.A5PrintCapabilities(
                Capabilities: null!,
                Imageable: null,
                CapabilitiesFallback: true,
                PageWidthDip: pageW,
                PageHeightDip: pageH,
                OriginWidthDip: 0,
                OriginHeightDip: 0,
                ExtentWidthDip: pageW,
                ExtentHeightDip: pageH,
                Orientation: orientation,
                ValidatedOrientation: System.Printing.PageOrientation.Portrait);

            var renderer = new WeighTicketRasterPrintRenderer();
            var logger = new WeighTicketPrintRenderingLogger();
            var document = renderer.CreateRasterDocument(model, caps, logger, out var nonWhiteRatio, out var adaptive);
            Assert.True(nonWhiteRatio > 0);

            var page = document.Pages[0].GetPageRoot(false) as System.Windows.Documents.FixedPage;
            Assert.NotNull(page);
            Assert.Equal(pageW, page!.Width, 1);
            Assert.Equal(pageH, page.Height, 1);
            Assert.NotNull(adaptive);
            Assert.True(adaptive!.Passed);
        });
    }
}
