using System.Printing;
using System.Windows.Media.Imaging;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class PrintA5AdaptiveFitOutcome
{
    public const string ValidationFailureMessage =
        "Nội dung phiếu chưa thể thu gọn an toàn trong vùng in của máy in.\nVui lòng kiểm tra cấu hình khổ giấy A5 và thử lại.";

    public bool Passed { get; init; }
    public bool AdaptiveRetry { get; init; }
    public RasterPlacement FinalPlacement { get; init; }
    public PrintA5FitResult FinalFit { get; init; }
    public ContentBoundsDip RawMainContentBounds { get; init; }
    public ContentBoundsDip? RawWatermarkBounds { get; init; }
    public A5PlacementValidationResult InitialValidation { get; init; } = null!;
    public A5PlacementValidationResult? AdjustedValidation { get; init; }
    public double InitialScale { get; init; }
    public double CorrectionScale { get; init; }
    public double AdjustedScale { get; init; }
    public A5PhysicalOrientationResolution? Orientation { get; init; }
    public IReadOnlyList<string> DiagnosticLines { get; init; } = [];
}

public static class PrintA5AdaptiveFitService
{
    public static PrintA5AdaptiveFitOutcome Resolve(
        BitmapSource ticketBitmap,
        PageImageableArea? imageableArea,
        double pageWidthDip,
        double pageHeightDip,
        double sourceWidthDip,
        double sourceHeightDip,
        bool scanWatermark,
        A5PhysicalOrientationResolution? orientation = null) =>
        Resolve(
            ticketBitmap,
            imageableArea,
            pageWidthDip,
            pageHeightDip,
            sourceWidthDip,
            sourceHeightDip,
            scanWatermark,
            orientation,
            diagnosticPrefix: null,
            rawMainContentOverride: null);

    public static PrintA5AdaptiveFitOutcome Resolve(
        BitmapSource ticketBitmap,
        PageImageableArea? imageableArea,
        double pageWidthDip,
        double pageHeightDip,
        double sourceWidthDip,
        double sourceHeightDip,
        bool scanWatermark,
        A5PhysicalOrientationResolution? orientation,
        IReadOnlyList<string>? diagnosticPrefix,
        ContentBoundsDip? rawMainContentOverride)
    {
        var initialPlacement = imageableArea is not null
            ? RasterPrintLayoutMapper.MapWithSafetyInset(imageableArea, sourceWidthDip, sourceHeightDip)
            : RasterPrintLayoutMapper.MapWithSafetyInsetFromExtents(
                0,
                0,
                pageWidthDip,
                pageHeightDip,
                sourceWidthDip,
                sourceHeightDip,
                capabilitiesFallback: true);

        return ResolveFromInitialPlacement(
            ticketBitmap,
            initialPlacement,
            sourceWidthDip,
            sourceHeightDip,
            scanWatermark,
            orientation,
            diagnosticPrefix,
            rawMainContentOverride);
    }

    public static PrintA5AdaptiveFitOutcome ResolveWithMainBounds(
        BitmapSource ticketBitmap,
        PageImageableArea? imageableArea,
        double pageWidthDip,
        double pageHeightDip,
        double sourceWidthDip,
        double sourceHeightDip,
        bool scanWatermark,
        A5PhysicalOrientationResolution? orientation,
        ContentBoundsDip rawMainContentBounds,
        ContentBoundsDip? rawWatermarkBounds = null)
    {
        var initialPlacement = imageableArea is not null
            ? RasterPrintLayoutMapper.MapWithSafetyInset(imageableArea, sourceWidthDip, sourceHeightDip)
            : RasterPrintLayoutMapper.MapWithSafetyInsetFromExtents(
                0,
                0,
                pageWidthDip,
                pageHeightDip,
                sourceWidthDip,
                sourceHeightDip,
                capabilitiesFallback: true);

        return ResolveFromInitialPlacement(
            ticketBitmap,
            initialPlacement,
            sourceWidthDip,
            sourceHeightDip,
            scanWatermark,
            orientation,
            BuildOrientationDiagnosticsForResolve(orientation),
            rawMainContentBounds,
            rawWatermarkBounds);
    }

    private static IReadOnlyList<string>? BuildOrientationDiagnosticsForResolve(A5PhysicalOrientationResolution? orientation)
    {
        if (orientation is null)
            return null;

        return
        [
            $"RequestedOrientation=Landscape",
            $"ActualPageWidthDip={orientation.ActualPageWidthDip:F2}",
            $"ActualPageHeightDip={orientation.ActualPageHeightDip:F2}",
            $"ActualPageAspect={orientation.ActualPageAspect}",
            $"DriverIgnoredLandscape={orientation.DriverIgnoredLandscape}",
            "SinglePassMapping=true"
        ];
    }

    public static PrintA5AdaptiveFitOutcome ResolveFromImageableExtents(
        BitmapSource ticketBitmap,
        double originWidthDip,
        double originHeightDip,
        double extentWidthDip,
        double extentHeightDip,
        double pageWidthDip,
        double pageHeightDip,
        double sourceWidthDip,
        double sourceHeightDip,
        bool scanWatermark)
    {
        var initialPlacement = RasterPrintLayoutMapper.MapWithSafetyInsetFromExtents(
            originWidthDip,
            originHeightDip,
            extentWidthDip,
            extentHeightDip,
            sourceWidthDip,
            sourceHeightDip);
        return ResolveFromInitialPlacement(
            ticketBitmap,
            initialPlacement,
            sourceWidthDip,
            sourceHeightDip,
            scanWatermark,
            orientation: null,
            diagnosticPrefix: null,
            rawMainContentOverride: null);
    }

    private static PrintA5AdaptiveFitOutcome ResolveFromInitialPlacement(
        BitmapSource ticketBitmap,
        RasterPlacement initialPlacement,
        double sourceWidthDip,
        double sourceHeightDip,
        bool scanWatermark,
        A5PhysicalOrientationResolution? orientation,
        IReadOnlyList<string>? diagnosticPrefix,
        ContentBoundsDip? rawMainContentOverride,
        ContentBoundsDip? rawWatermarkOverride = null)
    {
        var initialFit = ToFitResult(initialPlacement);

        var bounds = RasterContentBoundsAnalyzer.AnalyzeTicketBitmap(
            ticketBitmap,
            scanWatermark,
            sourceWidthDip,
            sourceHeightDip);
        var rawMain = rawMainContentOverride ?? bounds.MainContentBoundsDip;
        if (rawMain.IsEmpty)
            rawMain = ContentBoundsDip.FromSize(sourceWidthDip, sourceHeightDip);
        var watermarkBounds = rawWatermarkOverride ?? bounds.WatermarkBoundsDip;

        var diagnostics = new List<string>();
        if (diagnosticPrefix is not null)
            diagnostics.AddRange(diagnosticPrefix);

        if (orientation is not null)
        {
            diagnostics.Add($"PhysicalOrientationMode={orientation.Mode}");
            diagnostics.Add($"RotationDegrees={orientation.RotationDegrees}");
            diagnostics.Add($"RotatedLogicalSize={orientation.RotatedContentWidthDip:F2}x{orientation.RotatedContentHeightDip:F2}");
            diagnostics.Add($"ApplicationManagedRotation={orientation.RotationDegrees > 0}");
            diagnostics.Add("ApplicationManagedScaling=true");
            diagnostics.Add("DriverFitToPageRequested=false");
        }

        diagnostics.Add($"LogicalTemplateSize={WeighTicketPrintLayout.TicketLogicalWidthMm}x{WeighTicketPrintLayout.TicketLogicalHeightMm}mm");
        diagnostics.Add($"RawMainContentBounds={rawMain.Left:F2},{rawMain.Top:F2},{rawMain.Right:F2},{rawMain.Bottom:F2}");
        diagnostics.Add($"InitialScale={initialFit.FinalScale:F4}");
        diagnostics.Add($"InitialTranslate={initialFit.TranslateXDip:F2},{initialFit.TranslateYDip:F2}");

        if (watermarkBounds is { } watermark && !watermark.IsEmpty)
            diagnostics.Add($"RawWatermarkBounds={watermark.Left:F2},{watermark.Top:F2},{watermark.Right:F2},{watermark.Bottom:F2}");

        var initialValidation = A5PrintPlacementValidator.Validate(
            rawMain,
            watermarkBounds,
            initialFit.TranslateXDip,
            initialFit.TranslateYDip,
            initialFit.FinalScale,
            initialFit.TargetLeftDip,
            initialFit.TargetTopDip,
            initialFit.TargetWidthDip,
            initialFit.TargetHeightDip,
            attempt: 1);

        diagnostics.AddRange(initialValidation.Lines);

        if (initialValidation.Passed)
        {
            diagnostics.Add("AdaptiveRetry=false");
            diagnostics.Add("ValidationResult=PASS");
            return new PrintA5AdaptiveFitOutcome
            {
                Passed = true,
                AdaptiveRetry = false,
                FinalPlacement = initialPlacement,
                FinalFit = initialFit,
                RawMainContentBounds = rawMain,
                RawWatermarkBounds = watermarkBounds,
                InitialValidation = initialValidation,
                InitialScale = initialFit.FinalScale,
                CorrectionScale = 1.0,
                AdjustedScale = initialFit.FinalScale,
                Orientation = orientation,
                DiagnosticLines = diagnostics
            };
        }

        var adjustment = PrintA5AdaptiveFitCalculator.ComputeAdjustedPlacement(
            initialFit,
            rawMain,
            sourceWidthDip,
            sourceHeightDip);
        var adjustedFit = PrintA5AdaptiveFitCalculator.ToFitResult(initialFit, adjustment);
        var adjustedPlacement = RasterPrintLayoutMapper.FromFitResult(adjustedFit);

        diagnostics.Add("AdaptiveRetry=true");
        diagnostics.Add($"CorrectionScale={adjustment.CorrectionScale:F4}");
        diagnostics.Add($"AdjustedScale={adjustment.AdjustedScale:F4}");
        diagnostics.Add($"AdjustedTranslate={adjustment.TranslateXDip:F2},{adjustment.TranslateYDip:F2}");

        var adjustedValidation = A5PrintPlacementValidator.Validate(
            rawMain,
            watermarkBounds,
            adjustedFit.TranslateXDip,
            adjustedFit.TranslateYDip,
            adjustedFit.FinalScale,
            adjustedFit.TargetLeftDip,
            adjustedFit.TargetTopDip,
            adjustedFit.TargetWidthDip,
            adjustedFit.TargetHeightDip,
            attempt: 2);

        diagnostics.AddRange(adjustedValidation.Lines);
        diagnostics.Add(adjustedValidation.Passed ? "ValidationResult=PASS" : "ValidationResult=FAIL");

        return new PrintA5AdaptiveFitOutcome
        {
            Passed = adjustedValidation.Passed,
            AdaptiveRetry = true,
            FinalPlacement = adjustedPlacement,
            FinalFit = adjustedFit,
            RawMainContentBounds = rawMain,
            RawWatermarkBounds = watermarkBounds,
            InitialValidation = initialValidation,
            AdjustedValidation = adjustedValidation,
            InitialScale = initialFit.FinalScale,
            CorrectionScale = adjustment.CorrectionScale,
            AdjustedScale = adjustment.AdjustedScale,
            Orientation = orientation,
            DiagnosticLines = diagnostics
        };
    }

    private static PrintA5FitResult ToFitResult(RasterPlacement placement) =>
        new(
            placement.TargetLeftDip,
            placement.TargetTopDip,
            placement.TargetWidthDip,
            placement.TargetHeightDip,
            placement.ScaleFactor,
            placement.ScaleFactor,
            placement.ScaleFactor,
            placement.WidthDip,
            placement.HeightDip,
            placement.LeftDip,
            placement.TopDip,
            placement.SinglePassMapping,
            false);
}
