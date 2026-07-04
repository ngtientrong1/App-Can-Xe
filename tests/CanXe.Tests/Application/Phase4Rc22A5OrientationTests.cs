using CanXe.Domain.Services;
using Xunit;

namespace CanXe.Tests.Application;

public sealed class Phase4Rc22A5OrientationTests
{
    [Fact]
    public void NativeLandscape_WhenWidthGreaterOrEqualHeight()
    {
        var resolution = A5PhysicalOrientationResolver.Resolve(
            WeighTicketPrintLayout.A5LandscapePageWidthDip,
            WeighTicketPrintLayout.A5LandscapePageHeightDip,
            PageOrientationLabel.Landscape,
            PageOrientationLabel.Landscape);

        Assert.Equal(A5PhysicalOrientationMode.NativeLandscape, resolution.Mode);
        Assert.Equal(0, resolution.RotationDegrees);
        Assert.False(resolution.DriverIgnoredLandscape);
        Assert.Equal("Landscape", resolution.ActualPageAspect);
    }

    [Fact]
    public void PortraitFallback_WhenActualPageIsPortrait()
    {
        var resolution = A5PhysicalOrientationResolver.Resolve(
            WeighTicketPrintLayout.A5LandscapePageHeightDip,
            WeighTicketPrintLayout.A5LandscapePageWidthDip,
            PageOrientationLabel.Landscape,
            PageOrientationLabel.Portrait);

        Assert.Equal(A5PhysicalOrientationMode.PortraitDriverFallbackRotateClockwise, resolution.Mode);
        Assert.Equal(90, resolution.RotationDegrees);
        Assert.False(resolution.DriverIgnoredLandscape);
        Assert.Equal(WeighTicketPrintLayout.TicketLogicalHeightDip, resolution.RotatedContentWidthDip, 2);
        Assert.Equal(WeighTicketPrintLayout.TicketLogicalWidthDip, resolution.RotatedContentHeightDip, 2);
    }

    [Fact]
    public void DriverIgnoredLandscape_WhenValidatedLandscapeButPagePortrait()
    {
        var resolution = A5PhysicalOrientationResolver.Resolve(
            559.4,
            793.7,
            PageOrientationLabel.Landscape,
            PageOrientationLabel.Landscape);

        Assert.True(resolution.DriverIgnoredLandscape);
        Assert.Equal(A5PhysicalOrientationMode.PortraitDriverFallbackRotateClockwise, resolution.Mode);
    }

    [Fact]
    public void RotateBoundsClockwise90_SwapsAxes()
    {
        var raw = new ContentBoundsDip(10, 5, 200, 140);
        var rotated = A5PrintBoundsTransformer.RotateBoundsClockwise90(
            raw,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);

        Assert.True(rotated.Width > 0);
        Assert.True(rotated.Height > 0);
        Assert.NotEqual(raw.Left, rotated.Left);
    }

    [Fact]
    public void TransformBoundsForPrint_PortraitModeDiffersFromLandscape()
    {
        var raw = new ContentBoundsDip(10, 5, 200, 140);
        var landscape = A5PrintBoundsTransformer.TransformBoundsForPrint(
            raw,
            A5PhysicalOrientationMode.NativeLandscape,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip,
            20,
            15,
            0.9);
        var portrait = A5PrintBoundsTransformer.TransformBoundsForPrint(
            raw,
            A5PhysicalOrientationMode.PortraitDriverFallbackRotateClockwise,
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip,
            20,
            15,
            0.9);

        Assert.NotEqual(landscape.Left, portrait.Left, 1);
    }

    [Fact]
    public void PortraitFallbackScale_UsesRotatedDimensions()
    {
        var pageW = WeighTicketPrintLayout.A5LandscapePageHeightDip;
        var pageH = WeighTicketPrintLayout.A5LandscapePageWidthDip;
        var fit = PrintA5FitCalculator.ComputeFallback(
            pageW,
            pageH,
            WeighTicketPrintLayout.TicketLogicalHeightDip,
            WeighTicketPrintLayout.TicketLogicalWidthDip);

        Assert.True(fit.FinalScale <= 1.0);
        Assert.True(fit.FinalWidthDip <= fit.TargetWidthDip + WeighTicketPrintLayout.GeometryToleranceDip);
        Assert.True(fit.FinalHeightDip <= fit.TargetHeightDip + WeighTicketPrintLayout.GeometryToleranceDip);
    }
}
