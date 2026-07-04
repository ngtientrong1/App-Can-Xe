namespace CanXe.Domain.Services;

public static class A5PrintPlacementValidator
{
    public static ContentBoundsDip TransformBounds(
        ContentBoundsDip rawBounds,
        double translateXDip,
        double translateYDip,
        double finalScale) =>
        new(
            translateXDip + rawBounds.Left * finalScale,
            translateYDip + rawBounds.Top * finalScale,
            translateXDip + rawBounds.Right * finalScale,
            translateYDip + rawBounds.Bottom * finalScale);

    public static A5PlacementValidationResult Validate(
        ContentBoundsDip rawMainContentBounds,
        ContentBoundsDip? rawWatermarkBounds,
        double translateXDip,
        double translateYDip,
        double finalScale,
        double targetLeftDip,
        double targetTopDip,
        double targetWidthDip,
        double targetHeightDip,
        int attempt)
    {
        var transformed = TransformBounds(rawMainContentBounds, translateXDip, translateYDip, finalScale);
        var toleranceDip = WeighTicketPrintLayout.RasterAntialiasToleranceDip;
        var tolerancePx = WeighTicketPrintLayout.RasterAntialiasTolerancePx;
        var targetRight = targetLeftDip + targetWidthDip;
        var targetBottom = targetTopDip + targetHeightDip;

        var overflowLeftDip = Math.Max(0, targetLeftDip - transformed.Left);
        var overflowTopDip = Math.Max(0, targetTopDip - transformed.Top);
        var overflowRightDip = Math.Max(0, transformed.Right - targetRight);
        var overflowBottomDip = Math.Max(0, transformed.Bottom - targetBottom);

        var overflowLeftPx = PrintCoordinateConverter.DipToRasterPx(overflowLeftDip);
        var overflowTopPx = PrintCoordinateConverter.DipToRasterPx(overflowTopDip);
        var overflowRightPx = PrintCoordinateConverter.DipToRasterPx(overflowRightDip);
        var overflowBottomPx = PrintCoordinateConverter.DipToRasterPx(overflowBottomDip);

        var clearanceLeftDip = transformed.Left - targetLeftDip;
        var clearanceTopDip = transformed.Top - targetTopDip;
        var clearanceRightDip = targetRight - transformed.Right;
        var clearanceBottomDip = targetBottom - transformed.Bottom;

        var clearanceLeftPx = PrintCoordinateConverter.DipToRasterPx(clearanceLeftDip);
        var clearanceRightPx = PrintCoordinateConverter.DipToRasterPx(clearanceRightDip);
        var clearanceTopPx = PrintCoordinateConverter.DipToRasterPx(clearanceTopDip);
        var clearanceBottomPx = PrintCoordinateConverter.DipToRasterPx(clearanceBottomDip);

        var lines = new List<string>
        {
            $"ValidationAttempt={attempt}",
            $"CoordinateSystem=DIP_transformed_vs_safe_target",
            $"RawMainContentBounds={rawMainContentBounds.Left:F2},{rawMainContentBounds.Top:F2},{rawMainContentBounds.Right:F2},{rawMainContentBounds.Bottom:F2}",
            $"TransformedMainContentBounds={transformed.Left:F2},{transformed.Top:F2},{transformed.Right:F2},{transformed.Bottom:F2}",
            $"FinalSafeTargetRect={targetLeftDip:F2},{targetTopDip:F2},{targetWidthDip:F2},{targetHeightDip:F2}",
            $"InitialScale={finalScale:F4}",
            $"InitialTranslate={translateXDip:F2},{translateYDip:F2}",
            $"OverflowLeftPx={overflowLeftPx:F2}",
            $"OverflowTopPx={overflowTopPx:F2}",
            $"OverflowRightPx={overflowRightPx:F2}",
            $"OverflowBottomPx={overflowBottomPx:F2}",
            $"AntialiasTolerancePx={tolerancePx:F2}",
            $"FinalClearance L={clearanceLeftPx:F0} R={clearanceRightPx:F0} T={clearanceTopPx:F0} B={clearanceBottomPx:F0}"
        };

        if (rawWatermarkBounds is { } watermark && !watermark.IsEmpty)
            lines.Add($"RawWatermarkBounds={watermark.Left:F2},{watermark.Top:F2},{watermark.Right:F2},{watermark.Bottom:F2}");

        var failures = 0;
        if (overflowLeftPx > tolerancePx)
        {
            failures++;
            lines.Add($"FAIL: Left overflow {overflowLeftPx:F2}px > {tolerancePx:F2}px");
        }

        if (overflowTopPx > tolerancePx)
        {
            failures++;
            lines.Add($"FAIL: Top overflow {overflowTopPx:F2}px > {tolerancePx:F2}px");
        }

        if (overflowRightPx > tolerancePx)
        {
            failures++;
            lines.Add($"FAIL: Right overflow {overflowRightPx:F2}px > {tolerancePx:F2}px");
        }

        if (overflowBottomPx > tolerancePx)
        {
            failures++;
            lines.Add($"FAIL: Bottom overflow {overflowBottomPx:F2}px > {tolerancePx:F2}px");
        }

        if (clearanceLeftPx + tolerancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Left clearance {clearanceLeftPx:F0}px < {WeighTicketPrintLayout.MinRasterEdgeClearancePx}px");
        }

        if (clearanceRightPx + tolerancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Right clearance {clearanceRightPx:F0}px < {WeighTicketPrintLayout.MinRasterEdgeClearancePx}px");
        }

        if (clearanceTopPx + tolerancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Top clearance {clearanceTopPx:F0}px < {WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx}px");
        }

        if (clearanceBottomPx + tolerancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Bottom clearance {clearanceBottomPx:F0}px < {WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx}px");
        }

        lines.Add(failures == 0 ? "PASS" : "FAIL");

        return new A5PlacementValidationResult
        {
            Passed = failures == 0,
            Attempt = attempt,
            RawMainContentBounds = rawMainContentBounds,
            RawWatermarkBounds = rawWatermarkBounds,
            TransformedMainContentBounds = transformed,
            TranslateXDip = translateXDip,
            TranslateYDip = translateYDip,
            FinalScale = finalScale,
            TargetLeftDip = targetLeftDip,
            TargetTopDip = targetTopDip,
            TargetWidthDip = targetWidthDip,
            TargetHeightDip = targetHeightDip,
            OverflowLeftPx = overflowLeftPx,
            OverflowTopPx = overflowTopPx,
            OverflowRightPx = overflowRightPx,
            OverflowBottomPx = overflowBottomPx,
            AntialiasTolerancePx = tolerancePx,
            FinalClearanceLeftPx = clearanceLeftPx,
            FinalClearanceRightPx = clearanceRightPx,
            FinalClearanceTopPx = clearanceTopPx,
            FinalClearanceBottomPx = clearanceBottomPx,
            Lines = lines
        };
    }
}
