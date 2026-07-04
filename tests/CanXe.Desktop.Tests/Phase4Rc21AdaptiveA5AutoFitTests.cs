using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Services;
using CanXe.Desktop.Tests.Support;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Tests;

[Collection("WpfSta")]
public sealed class Phase4Rc21AdaptiveA5AutoFitTests
{
    private readonly WpfSmokeFixture _fixture;

    public Phase4Rc21AdaptiveA5AutoFitTests(WpfSmokeFixture fixture) => _fixture = fixture;

    [Fact]
    public void RasterA5_AdaptiveFit_PassesForSampleTicket()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var renderer = new WeighTicketRasterPrintRenderer();
            var logger = new WeighTicketPrintRenderingLogger();
            renderer.CreateRasterA5SingleDocument(model, null, logger, out var nonWhiteRatio, out var adaptive);
            Assert.NotNull(adaptive);
            Assert.True(adaptive!.Passed);
            Assert.True(nonWhiteRatio > 0);
            Assert.True(adaptive.FinalFit.FinalScale <= 1.0);
        });
    }

    [Fact]
    public void AdaptiveFit_FullPageImageable_StillHasSafetyInset()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var adaptive = PrintA5AdaptiveFitService.Resolve(
                bitmap,
                imageableArea: null,
                WeighTicketPrintLayout.A5LandscapePageWidthDip,
                WeighTicketPrintLayout.A5LandscapePageHeightDip,
                WeighTicketPrintLayout.TicketLogicalWidthDip,
                WeighTicketPrintLayout.TicketLogicalHeightDip,
                scanWatermark: false);

            Assert.True(adaptive.Passed);
            Assert.True(adaptive.FinalFit.TargetLeftDip >= WeighTicketPrintLayout.PrinterImageableSafetyInsetHorizontalDip - 0.1);
            Assert.True(adaptive.FinalFit.TargetTopDip >= WeighTicketPrintLayout.PrinterImageableSafetyInsetVerticalDip - 0.1);
        });
    }

    [Fact]
    public void AdaptiveFit_AsymmetricMargin_StillPasses()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var pageW = WeighTicketPrintLayout.A5LandscapePageWidthDip;
            var pageH = WeighTicketPrintLayout.A5LandscapePageHeightDip;
            var leftOrigin = WeighTicketPrintLayout.MmToDip(6);
            var topOrigin = WeighTicketPrintLayout.MmToDip(3);
            var extentW = pageW - leftOrigin - WeighTicketPrintLayout.MmToDip(12);
            var extentH = pageH - topOrigin - WeighTicketPrintLayout.MmToDip(8);
            var adaptive = PrintA5AdaptiveFitService.ResolveFromImageableExtents(
                bitmap,
                leftOrigin,
                topOrigin,
                extentW,
                extentH,
                pageW,
                pageH,
                WeighTicketPrintLayout.TicketLogicalWidthDip,
                WeighTicketPrintLayout.TicketLogicalHeightDip,
                scanWatermark: false);

            Assert.True(adaptive.Passed);
        });
    }

    [Fact]
    public void NormalTicket_HasNoWatermarkBounds()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var bounds = RasterContentBoundsAnalyzer.AnalyzeTicketBitmap(bitmap, scanWatermark: true);
            Assert.Null(bounds.WatermarkBoundsDip);
        });
    }

    [Fact]
    public void ReprintTicket_CanDetectWatermarkBounds_WithoutBlockingMainContent()
    {
        _fixture.Invoke(_ =>
        {
            var model = WeighTicketPrintModelMapper.FromDetail(
                new WeighTicketDetailDto
                {
                    Id = 1,
                    DisplayNumber = "15/06",
                    TicketDateTime = DateTimeOffset.UtcNow
                },
                new StationSettingsDto { StationName = "TRẠM CÂN TIẾN TRỌNG" },
                new PrintSettingsDto { ShowReprintWatermark = true },
                isReprint: true);
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var bounds = RasterContentBoundsAnalyzer.AnalyzeTicketBitmap(bitmap, scanWatermark: true);
            Assert.False(bounds.MainContentBoundsDip.IsEmpty);

            var adaptive = PrintA5AdaptiveFitService.Resolve(
                bitmap,
                null,
                WeighTicketPrintLayout.A5LandscapePageWidthDip,
                WeighTicketPrintLayout.A5LandscapePageHeightDip,
                WeighTicketPrintLayout.TicketLogicalWidthDip,
                WeighTicketPrintLayout.TicketLogicalHeightDip,
                scanWatermark: true);
            Assert.True(adaptive.Passed);
            if (bounds.WatermarkBoundsDip is { } watermark)
                Assert.False(watermark.IsEmpty);
        });
    }

    [Fact]
    public void ValidationFailure_UsesSpecificMessage()
    {
        Assert.Contains(
            "chưa thể thu gọn",
            PrintA5AdaptiveFitOutcome.ValidationFailureMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AdaptiveRetry_AtMostOnce_WhenInitialFails()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var pageW = WeighTicketPrintLayout.A5LandscapePageWidthDip;
            var pageH = WeighTicketPrintLayout.A5LandscapePageHeightDip;
            var adaptive = PrintA5AdaptiveFitService.ResolveFromImageableExtents(
                bitmap,
                pageW * 0.25,
                pageH * 0.25,
                pageW * 0.35,
                pageH * 0.35,
                pageW,
                pageH,
                WeighTicketPrintLayout.TicketLogicalWidthDip,
                WeighTicketPrintLayout.TicketLogicalHeightDip,
                scanWatermark: false);

            if (!adaptive.InitialValidation.Passed)
            {
                Assert.True(adaptive.AdaptiveRetry);
                Assert.NotNull(adaptive.AdjustedValidation);
            }
        });
    }

    [Fact]
    public void FinalClearance_MeetsMinimum_OnSampleTicket()
    {
        _fixture.Invoke(_ =>
        {
            var model = PrintLayoutGeometryTestRunner.BuildSampleModelPublic();
            var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
            var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
            var adaptive = PrintA5AdaptiveFitService.Resolve(
                bitmap,
                null,
                WeighTicketPrintLayout.A5LandscapePageWidthDip,
                WeighTicketPrintLayout.A5LandscapePageHeightDip,
                WeighTicketPrintLayout.TicketLogicalWidthDip,
                WeighTicketPrintLayout.TicketLogicalHeightDip,
                scanWatermark: false);
            var final = adaptive.AdjustedValidation ?? adaptive.InitialValidation;
            Assert.True(final.FinalClearanceLeftPx + 0.01 >= WeighTicketPrintLayout.MinRasterEdgeClearancePx - WeighTicketPrintLayout.RasterAntialiasTolerancePx);
            Assert.True(final.FinalClearanceRightPx + 0.01 >= WeighTicketPrintLayout.MinRasterEdgeClearancePx - WeighTicketPrintLayout.RasterAntialiasTolerancePx);
            Assert.True(final.FinalClearanceTopPx + 0.01 >= WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx - WeighTicketPrintLayout.RasterAntialiasTolerancePx);
            Assert.True(final.FinalClearanceBottomPx + 0.01 >= WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx - WeighTicketPrintLayout.RasterAntialiasTolerancePx);
        });
    }
}
