namespace CanXe.Domain.Services;

public sealed class A5PhysicalOrientationResolution
{
    public A5PhysicalOrientationMode Mode { get; init; }
    public bool DriverIgnoredLandscape { get; init; }
    public string ActualPageAspect { get; init; } = "Landscape";
    public int RotationDegrees { get; init; }
    public double ActualPageWidthDip { get; init; }
    public double ActualPageHeightDip { get; init; }
    public double RotatedContentWidthDip { get; init; }
    public double RotatedContentHeightDip { get; init; }
}
