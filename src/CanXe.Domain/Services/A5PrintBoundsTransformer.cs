namespace CanXe.Domain.Services;

public static class A5PrintBoundsTransformer
{
    public static ContentBoundsDip RotateBoundsClockwise90(
        ContentBoundsDip landscapeBounds,
        double landscapeWidthDip,
        double landscapeHeightDip) =>
        new(
            landscapeHeightDip - landscapeBounds.Bottom,
            landscapeBounds.Left,
            landscapeHeightDip - landscapeBounds.Top,
            landscapeBounds.Right);

    public static ContentBoundsDip TransformBoundsForPrint(
        ContentBoundsDip rawLandscapeBounds,
        A5PhysicalOrientationMode orientationMode,
        double landscapeWidthDip,
        double landscapeHeightDip,
        double translateXDip,
        double translateYDip,
        double finalScale)
    {
        var contentBounds = orientationMode == A5PhysicalOrientationMode.NativeLandscape
            ? rawLandscapeBounds
            : RotateBoundsClockwise90(rawLandscapeBounds, landscapeWidthDip, landscapeHeightDip);

        return A5PrintPlacementValidator.TransformBounds(
            contentBounds,
            translateXDip,
            translateYDip,
            finalScale);
    }
}
