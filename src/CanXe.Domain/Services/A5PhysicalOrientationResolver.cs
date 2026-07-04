namespace CanXe.Domain.Services;

public static class A5PhysicalOrientationResolver
{
    public static A5PhysicalOrientationResolution Resolve(
        double actualPageWidthDip,
        double actualPageHeightDip,
        PageOrientationLabel requestedOrientation,
        PageOrientationLabel? validatedOrientation)
    {
        var nativeLandscape = actualPageWidthDip >= actualPageHeightDip;
        var driverIgnoredLandscape =
            validatedOrientation == PageOrientationLabel.Landscape && !nativeLandscape;

        var mode = nativeLandscape
            ? A5PhysicalOrientationMode.NativeLandscape
            : A5PhysicalOrientationMode.PortraitDriverFallbackRotateClockwise;

        var rotationDegrees = mode == A5PhysicalOrientationMode.NativeLandscape ? 0 : 90;
        var rotatedWidth = mode == A5PhysicalOrientationMode.NativeLandscape
            ? WeighTicketPrintLayout.TicketLogicalWidthDip
            : WeighTicketPrintLayout.TicketLogicalHeightDip;
        var rotatedHeight = mode == A5PhysicalOrientationMode.NativeLandscape
            ? WeighTicketPrintLayout.TicketLogicalHeightDip
            : WeighTicketPrintLayout.TicketLogicalWidthDip;

        return new A5PhysicalOrientationResolution
        {
            Mode = mode,
            DriverIgnoredLandscape = driverIgnoredLandscape,
            ActualPageAspect = nativeLandscape ? "Landscape" : "Portrait",
            RotationDegrees = rotationDegrees,
            ActualPageWidthDip = actualPageWidthDip,
            ActualPageHeightDip = actualPageHeightDip,
            RotatedContentWidthDip = rotatedWidth,
            RotatedContentHeightDip = rotatedHeight
        };
    }
}

public enum PageOrientationLabel
{
    Unknown,
    Portrait,
    Landscape
}
