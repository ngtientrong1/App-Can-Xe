using CanXe.Domain.Services;
using Xunit;

namespace CanXe.Tests.Application;

public sealed class Phase4Rc21DriverMarginSimulationTests
{
    [Fact]
    public void FullPageImageable_With3mmMargin_StillFitsTemplate148_5()
    {
        var pageW = WeighTicketPrintLayout.A5LandscapePageWidthDip;
        var pageH = WeighTicketPrintLayout.A5LandscapePageHeightDip;
        var margin = WeighTicketPrintLayout.MmToDip(3);
        var fit = PrintA5FitCalculator.Compute(
            margin,
            margin,
            pageW - margin * 2,
            pageH - margin * 2,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);

        Assert.True(fit.FinalScale <= 1.0);
        Assert.True(fit.FinalHeightDip <= fit.TargetHeightDip + WeighTicketPrintLayout.GeometryToleranceDip);
    }

    [Fact]
    public void AsymmetricMargin_StillProducesCenteredPlacement()
    {
        var pageW = WeighTicketPrintLayout.A5LandscapePageWidthDip;
        var pageH = WeighTicketPrintLayout.A5LandscapePageHeightDip;
        var left = WeighTicketPrintLayout.MmToDip(6);
        var top = WeighTicketPrintLayout.MmToDip(3);
        var extentW = pageW - left - WeighTicketPrintLayout.MmToDip(12);
        var extentH = pageH - top - WeighTicketPrintLayout.MmToDip(8);
        var fit = PrintA5FitCalculator.Compute(
            left,
            top,
            extentW,
            extentH,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);

        var leftGap = fit.TranslateXDip - fit.TargetLeftDip;
        var rightGap = fit.TargetLeftDip + fit.TargetWidthDip - (fit.TranslateXDip + fit.FinalWidthDip);
        Assert.Equal(leftGap, rightGap, 2);
        Assert.True(fit.FinalScale <= 1.0);
    }

    [Fact]
    public void DriverDipJitter_WithinAntialiasTolerance_PassesValidation()
    {
        var raw = new ContentBoundsDip(0, 0, 200, 130);
        var targetLeft = 10;
        var targetTop = 8;
        var jitterDip = WeighTicketPrintLayout.RasterAntialiasToleranceDip * 0.5;
        var result = A5PrintPlacementValidator.Validate(
            raw,
            null,
            targetLeft - jitterDip,
            targetTop,
            0.9,
            targetLeft,
            targetTop,
            180,
            120,
            attempt: 1);

        Assert.True(result.OverflowLeftPx <= WeighTicketPrintLayout.RasterAntialiasTolerancePx);
    }

    [Fact]
    public void AdaptiveCorrection_NeverIncreasesScale()
    {
        var initial = PrintA5FitCalculator.ComputeFallback(
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
        Assert.True(adjustment.CorrectionScale <= 1.0);
    }
}
