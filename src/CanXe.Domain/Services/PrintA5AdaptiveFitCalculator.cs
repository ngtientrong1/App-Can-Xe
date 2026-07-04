namespace CanXe.Domain.Services;

public readonly record struct AdaptiveFitAdjustment(
    double CorrectionScale,
    double AdjustedScale,
    double TranslateXDip,
    double TranslateYDip,
    double FinalWidthDip,
    double FinalHeightDip);

public static class PrintA5AdaptiveFitCalculator
{
    public static double ComputeCorrectionScale(
        ContentBoundsDip rawMainContentBounds,
        double initialScale,
        double targetWidthDip,
        double targetHeightDip,
        double extraClearanceDip = 0)
    {
        if (rawMainContentBounds.IsEmpty || initialScale <= 0)
            return 1.0;

        var availableWidth = targetWidthDip - 2 * extraClearanceDip;
        var availableHeight = targetHeightDip - 2 * extraClearanceDip;
        if (availableWidth <= 0 || availableHeight <= 0)
            return 1.0;

        var contentWidthAfterInitial = rawMainContentBounds.Width * initialScale;
        var contentHeightAfterInitial = rawMainContentBounds.Height * initialScale;
        if (contentWidthAfterInitial <= 0 || contentHeightAfterInitial <= 0)
            return 1.0;

        var correctionX = availableWidth / contentWidthAfterInitial;
        var correctionY = availableHeight / contentHeightAfterInitial;
        return Math.Min(1.0, Math.Min(correctionX, correctionY));
    }

    public static AdaptiveFitAdjustment ComputeAdjustedPlacement(
        PrintA5FitResult initialFit,
        ContentBoundsDip rawMainContentBounds,
        double contentWidthDip,
        double contentHeightDip)
    {
        var correctionScale = ComputeCorrectionScale(
            rawMainContentBounds,
            initialFit.FinalScale,
            initialFit.TargetWidthDip,
            initialFit.TargetHeightDip);

        var adjustedScale = initialFit.FinalScale * correctionScale * WeighTicketPrintLayout.AutoFitReserve;
        adjustedScale = Math.Min(adjustedScale, initialFit.FinalScale);
        adjustedScale = Math.Min(adjustedScale, 1.0);

        var finalWidth = contentWidthDip * adjustedScale;
        var finalHeight = contentHeightDip * adjustedScale;
        var translateX = initialFit.TargetLeftDip + (initialFit.TargetWidthDip - finalWidth) / 2;
        var translateY = initialFit.TargetTopDip + (initialFit.TargetHeightDip - finalHeight) / 2;

        return new AdaptiveFitAdjustment(
            correctionScale,
            adjustedScale,
            translateX,
            translateY,
            finalWidth,
            finalHeight);
    }

    public static PrintA5FitResult ToFitResult(PrintA5FitResult initialFit, AdaptiveFitAdjustment adjustment) =>
        new(
            initialFit.TargetLeftDip,
            initialFit.TargetTopDip,
            initialFit.TargetWidthDip,
            initialFit.TargetHeightDip,
            initialFit.ScaleX,
            initialFit.ScaleY,
            adjustment.AdjustedScale,
            adjustment.FinalWidthDip,
            adjustment.FinalHeightDip,
            adjustment.TranslateXDip,
            adjustment.TranslateYDip,
            initialFit.SinglePassMapping,
            initialFit.CapabilitiesFallback);

    public static ContentBoundsDip ComputeMediaNormalizedContentBounds(
        ContentBoundsDip rawTicketBounds,
        double ticketLogicalHeightDip,
        double pageLogicalHeightDip)
    {
        if (pageLogicalHeightDip <= 0 || ticketLogicalHeightDip <= 0)
            return rawTicketBounds;

        if (Math.Abs(ticketLogicalHeightDip - pageLogicalHeightDip) < WeighTicketPrintLayout.GeometryToleranceDip)
            return rawTicketBounds;

        var mediaScale = pageLogicalHeightDip / ticketLogicalHeightDip;
        return new ContentBoundsDip(
            rawTicketBounds.Left * mediaScale,
            rawTicketBounds.Top * mediaScale,
            rawTicketBounds.Right * mediaScale,
            rawTicketBounds.Bottom * mediaScale);
    }
}
