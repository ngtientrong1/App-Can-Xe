using CanXe.Domain.Services;
using Xunit;

namespace CanXe.Tests.Application;

public sealed class Phase4Rc21AdaptiveA5AutoFitTests
{
    [Fact]
    public void Validator_UsesTransformedBounds_NotRawBounds()
    {
        var raw = new ContentBoundsDip(10, 5, 200, 140);
        var result = A5PrintPlacementValidator.Validate(
            raw,
            rawWatermarkBounds: null,
            translateXDip: 20,
            translateYDip: 15,
            finalScale: 0.95,
            targetLeftDip: 10,
            targetTopDip: 8,
            targetWidthDip: 180,
            targetHeightDip: 120,
            attempt: 1);

        var expected = A5PrintPlacementValidator.TransformBounds(raw, 20, 15, 0.95);
        Assert.Equal(expected.Left, result.TransformedMainContentBounds.Left, 2);
        Assert.Equal(expected.Right, result.TransformedMainContentBounds.Right, 2);
    }

    [Fact]
    public void CoordinateConverter_DoesNotMixMmAndDip()
    {
        var dip = 96;
        var px = PrintCoordinateConverter.DipToRasterPx(dip);
        Assert.Equal(300, px, 0);
        Assert.Equal(dip, PrintCoordinateConverter.RasterPxToDip(px), 2);
    }

    [Fact]
    public void A5Media148mm_ScalesTemplate148_5mm()
    {
        var contentW = WeighTicketPrintLayout.TicketLogicalWidthDip;
        var contentH = WeighTicketPrintLayout.TicketLogicalHeightDip;
        var pageH = WeighTicketPrintLayout.A5LandscapePageHeightDip;
        var fit = PrintA5FitCalculator.ComputeFallback(
            WeighTicketPrintLayout.A5LandscapePageWidthDip,
            pageH,
            contentW,
            contentH);

        Assert.True(fit.FinalScale < 1.0);
        Assert.True(fit.FinalHeightDip <= fit.TargetHeightDip + WeighTicketPrintLayout.GeometryToleranceDip);
    }

    [Fact]
    public void AntialiasTolerance_2px_DoesNotFailBorderlineOverflow()
    {
        var raw = ContentBoundsDip.FromSize(200, 130);
        var targetLeft = 10;
        var targetTop = 8;
        var targetWidth = 180;
        var targetHeight = 120;
        var toleranceDip = WeighTicketPrintLayout.RasterAntialiasToleranceDip;
        var scale = 0.9;
        var translateX = targetLeft - toleranceDip / 2;
        var translateY = targetTop;

        var result = A5PrintPlacementValidator.Validate(
            raw,
            null,
            translateX,
            translateY,
            scale,
            targetLeft,
            targetTop,
            targetWidth,
            targetHeight,
            attempt: 1);

        Assert.True(result.OverflowLeftPx <= WeighTicketPrintLayout.RasterAntialiasTolerancePx + 0.01);
    }

    [Fact]
    public void TrueOverflow_FailsValidation()
    {
        var raw = ContentBoundsDip.FromSize(200, 130);
        var result = A5PrintPlacementValidator.Validate(
            raw,
            null,
            translateXDip: 0,
            translateYDip: 0,
            finalScale: 1.0,
            targetLeftDip: 20,
            targetTopDip: 20,
            targetWidthDip: 100,
            targetHeightDip: 80,
            attempt: 1);

        Assert.False(result.Passed);
        Assert.True(result.OverflowRightPx > WeighTicketPrintLayout.RasterAntialiasTolerancePx);
    }

    [Fact]
    public void AdaptiveCorrection_ReducesUniformScale()
    {
        var initial = PrintA5FitCalculator.Compute(
            0,
            0,
            WeighTicketPrintLayout.A5LandscapePageWidthDip,
            WeighTicketPrintLayout.A5LandscapePageHeightDip,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);
        var raw = ContentBoundsDip.FromSize(
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);
        var adjustment = PrintA5AdaptiveFitCalculator.ComputeAdjustedPlacement(
            initial,
            raw,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);

        Assert.True(adjustment.AdjustedScale <= initial.FinalScale);
        Assert.True(adjustment.AdjustedScale <= 1.0);
    }

    [Fact]
    public void AdaptiveScale_NeverExceedsInitialOrOne()
    {
        var initial = PrintA5FitCalculator.Compute(0, 0, 500, 400, 200, 140);
        var raw = ContentBoundsDip.FromSize(180, 120);
        var adjustment = PrintA5AdaptiveFitCalculator.ComputeAdjustedPlacement(
            initial,
            raw,
            200,
            140);

        Assert.True(adjustment.AdjustedScale <= 1.0);
        Assert.True(adjustment.AdjustedScale <= initial.FinalScale);
    }

    [Fact]
    public void AdaptiveFit_ReCentersInTarget()
    {
        var initial = PrintA5FitCalculator.Compute(5, 5, 190, 130, 200, 140);
        var raw = ContentBoundsDip.FromSize(200, 140);
        var adjustment = PrintA5AdaptiveFitCalculator.ComputeAdjustedPlacement(
            initial,
            raw,
            200,
            140);
        var leftGap = adjustment.TranslateXDip - initial.TargetLeftDip;
        var rightGap = initial.TargetLeftDip + initial.TargetWidthDip - (adjustment.TranslateXDip + adjustment.FinalWidthDip);
        Assert.Equal(leftGap, rightGap, 2);
    }

    [Fact]
    public void CorrectionScale_IsMinOfAxisRatios_CappedAtOne()
    {
        var raw = new ContentBoundsDip(0, 0, 200, 100);
        var correction = PrintA5AdaptiveFitCalculator.ComputeCorrectionScale(raw, 0.9, 180, 120);
        Assert.True(correction <= 1.0);
        Assert.True(correction > 0);
    }

    [Fact]
    public void MediaNormalization_Handles148_vs_148_5()
    {
        var raw = new ContentBoundsDip(0, 0, 210, 148.5);
        var normalized = PrintA5AdaptiveFitCalculator.ComputeMediaNormalizedContentBounds(
            raw,
            WeighTicketPrintLayout.TicketLogicalHeightDip,
            WeighTicketPrintLayout.A5LandscapePageHeightDip);

        Assert.True(normalized.Bottom < raw.Bottom);
        Assert.Equal(148.0 / 148.5, normalized.Bottom / raw.Bottom, 3);
    }

    [Fact]
    public void AutoFitReserve_Is985()
    {
        Assert.Equal(0.985, WeighTicketPrintLayout.AutoFitReserve, 3);
    }

    [Fact]
    public void AntialiasTolerance_Is_0_64_Dip()
    {
        Assert.Equal(2.0, WeighTicketPrintLayout.RasterAntialiasTolerancePx, 2);
        Assert.Equal(0.64, WeighTicketPrintLayout.RasterAntialiasToleranceDip, 2);
    }
}
