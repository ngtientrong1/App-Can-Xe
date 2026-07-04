namespace CanXe.Domain.Services;

public sealed class A5PlacementValidationResult
{
    public bool Passed { get; init; }
    public int Attempt { get; init; }
    public ContentBoundsDip RawMainContentBounds { get; init; }
    public ContentBoundsDip? RawWatermarkBounds { get; init; }
    public ContentBoundsDip TransformedMainContentBounds { get; init; }
    public double TranslateXDip { get; init; }
    public double TranslateYDip { get; init; }
    public double FinalScale { get; init; }
    public double TargetLeftDip { get; init; }
    public double TargetTopDip { get; init; }
    public double TargetWidthDip { get; init; }
    public double TargetHeightDip { get; init; }
    public double OverflowLeftPx { get; init; }
    public double OverflowTopPx { get; init; }
    public double OverflowRightPx { get; init; }
    public double OverflowBottomPx { get; init; }
    public double AntialiasTolerancePx { get; init; }
    public double FinalClearanceLeftPx { get; init; }
    public double FinalClearanceRightPx { get; init; }
    public double FinalClearanceTopPx { get; init; }
    public double FinalClearanceBottomPx { get; init; }
    public IReadOnlyList<string> Lines { get; init; } = [];
}
