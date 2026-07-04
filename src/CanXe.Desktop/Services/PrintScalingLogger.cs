using System.IO;
using System.Printing;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class PrintScalingLogger
{
    private static string LogPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs",
            "print-scaling.log");

    public void LogJob(
        long jobId,
        string printerName,
        string? driverName,
        PrintLayoutMode layoutMode,
        PageMediaSizeName requestedMedia,
        PageOrientation requestedOrientation,
        double actualMediaWidthDip,
        double actualMediaHeightDip,
        PageImageableArea? imageable,
        PrintA5FitResult fit,
        int bitmapWidthPx,
        int bitmapHeightPx,
        double leftEdgeClearancePx,
        double rightEdgeClearancePx,
        double topEdgeClearancePx,
        double bottomEdgeClearancePx,
        string status) =>
        LogJob(
            jobId,
            printerName,
            driverName,
            layoutMode,
            requestedMedia,
            requestedOrientation,
            actualMediaWidthDip,
            actualMediaHeightDip,
            imageable,
            fit,
            adaptiveOutcome: null,
            bitmapWidthPx,
            bitmapHeightPx,
            leftEdgeClearancePx,
            rightEdgeClearancePx,
            topEdgeClearancePx,
            bottomEdgeClearancePx,
            spoolSubmissionResult: status);

    public void LogJob(
        long jobId,
        string printerName,
        string? driverName,
        PrintLayoutMode layoutMode,
        PageMediaSizeName requestedMedia,
        PageOrientation requestedOrientation,
        double actualMediaWidthDip,
        double actualMediaHeightDip,
        PageImageableArea? imageable,
        PrintA5FitResult fit,
        PrintA5AdaptiveFitOutcome? adaptiveOutcome,
        int bitmapWidthPx,
        int bitmapHeightPx,
        double leftEdgeClearancePx,
        double rightEdgeClearancePx,
        double topEdgeClearancePx,
        double bottomEdgeClearancePx,
        string spoolSubmissionResult) =>
        LogJob(
            jobId,
            printerName,
            driverName,
            layoutMode,
            requestedMedia,
            requestedOrientation,
            actualMediaWidthDip,
            actualMediaHeightDip,
            imageable,
            fit,
            adaptiveOutcome,
            orientation: null,
            validatedOrientation: null,
            bitmapWidthPx,
            bitmapHeightPx,
            leftEdgeClearancePx,
            rightEdgeClearancePx,
            topEdgeClearancePx,
            bottomEdgeClearancePx,
            spoolSubmissionResult);

    public void LogJob(
        long jobId,
        string printerName,
        string? driverName,
        PrintLayoutMode layoutMode,
        PageMediaSizeName requestedMedia,
        PageOrientation requestedOrientation,
        double actualMediaWidthDip,
        double actualMediaHeightDip,
        PageImageableArea? imageable,
        PrintA5FitResult fit,
        PrintA5AdaptiveFitOutcome? adaptiveOutcome,
        A5PhysicalOrientationResolution? orientation,
        PageOrientation? validatedOrientation,
        int bitmapWidthPx,
        int bitmapHeightPx,
        double leftEdgeClearancePx,
        double rightEdgeClearancePx,
        double topEdgeClearancePx,
        double bottomEdgeClearancePx,
        string spoolSubmissionResult)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);

            var lines = new List<string>
            {
                $"[{DateTimeOffset.Now:O}] job={jobId} status={spoolSubmissionResult}",
                $"Printer={printerName}",
                $"Driver={driverName ?? "unknown"}",
                $"LayoutMode={layoutMode}",
                $"RequestedMedia={requestedMedia}",
                $"RequestedOrientation={requestedOrientation}",
                $"ActualMediaWidthDip={actualMediaWidthDip:F2}",
                $"ActualMediaHeightDip={actualMediaHeightDip:F2}",
                $"ValidatedOrientation={validatedOrientation}"
            };

            if (orientation is not null)
            {
                lines.Add($"ActualPageAspect={orientation.ActualPageAspect}");
                lines.Add($"DriverIgnoredLandscape={orientation.DriverIgnoredLandscape}");
                lines.Add($"PhysicalOrientationMode={orientation.Mode}");
                lines.Add($"RotationDegrees={orientation.RotationDegrees}");
                lines.Add($"RotatedLogicalSize={orientation.RotatedContentWidthDip:F2}x{orientation.RotatedContentHeightDip:F2}");
                lines.Add($"ApplicationManagedRotation={orientation.RotationDegrees > 0}");
                lines.Add("ApplicationManagedScaling=true");
                lines.Add("DriverFitToPageRequested=false");
            }

            if (imageable is not null)
            {
                lines.Add($"ImageableArea origin={imageable.OriginWidth:F2},{imageable.OriginHeight:F2} extent={imageable.ExtentWidth:F2},{imageable.ExtentHeight:F2}");
            }

            lines.Add($"SafeTargetRect={fit.TargetLeftDip:F2},{fit.TargetTopDip:F2},{fit.TargetWidthDip:F2},{fit.TargetHeightDip:F2}");
            lines.Add($"LogicalTemplateSize={WeighTicketPrintLayout.TicketLogicalWidthMm}x{WeighTicketPrintLayout.TicketLogicalHeightMm}mm");

            if (adaptiveOutcome is not null)
            {
                var raw = adaptiveOutcome.RawMainContentBounds;
                lines.Add($"RawMainContentBounds={raw.Left:F2},{raw.Top:F2},{raw.Right:F2},{raw.Bottom:F2}");
                if (adaptiveOutcome.RawWatermarkBounds is { } watermark && !watermark.IsEmpty)
                    lines.Add($"RawWatermarkBounds={watermark.Left:F2},{watermark.Top:F2},{watermark.Right:F2},{watermark.Bottom:F2}");
                lines.Add($"InitialScale={adaptiveOutcome.InitialScale:F4}");
                lines.Add($"InitialTranslate={adaptiveOutcome.InitialValidation.TranslateXDip:F2},{adaptiveOutcome.InitialValidation.TranslateYDip:F2}");
                var initialTransformed = adaptiveOutcome.InitialValidation.TransformedMainContentBounds;
                lines.Add($"InitialTransformedBounds={initialTransformed.Left:F2},{initialTransformed.Top:F2},{initialTransformed.Right:F2},{initialTransformed.Bottom:F2}");
                lines.Add($"InitialOverflow L={adaptiveOutcome.InitialValidation.OverflowLeftPx:F2} T={adaptiveOutcome.InitialValidation.OverflowTopPx:F2} R={adaptiveOutcome.InitialValidation.OverflowRightPx:F2} B={adaptiveOutcome.InitialValidation.OverflowBottomPx:F2}");
                lines.Add($"AntialiasTolerance={adaptiveOutcome.InitialValidation.AntialiasTolerancePx:F2}px");
                lines.Add($"AdaptiveRetry={adaptiveOutcome.AdaptiveRetry}");
                lines.Add($"CorrectionScale={adaptiveOutcome.CorrectionScale:F4}");
                lines.Add($"AdjustedScale={adaptiveOutcome.AdjustedScale:F4}");
                if (adaptiveOutcome.AdjustedValidation is not null)
                {
                    lines.Add($"AdjustedTranslate={adaptiveOutcome.AdjustedValidation.TranslateXDip:F2},{adaptiveOutcome.AdjustedValidation.TranslateYDip:F2}");
                    var adjustedTransformed = adaptiveOutcome.AdjustedValidation.TransformedMainContentBounds;
                    lines.Add($"AdjustedTransformedBounds={adjustedTransformed.Left:F2},{adjustedTransformed.Top:F2},{adjustedTransformed.Right:F2},{adjustedTransformed.Bottom:F2}");
                    lines.Add($"ValidationResult={(adaptiveOutcome.Passed ? "PASS" : "FAIL")}");
                }

                var finalValidation = adaptiveOutcome.AdjustedValidation ?? adaptiveOutcome.InitialValidation;
                lines.Add($"FinalClearance L={finalValidation.FinalClearanceLeftPx:F0} R={finalValidation.FinalClearanceRightPx:F0} T={finalValidation.FinalClearanceTopPx:F0} B={finalValidation.FinalClearanceBottomPx:F0}");
            }

            lines.Add($"ScaleX={fit.ScaleX:F4}");
            lines.Add($"ScaleY={fit.ScaleY:F4}");
            lines.Add($"FinalScale={fit.FinalScale:F4}");
            lines.Add($"FinalSize={fit.FinalWidthDip:F2}x{fit.FinalHeightDip:F2}");
            lines.Add($"Translate={fit.TranslateXDip:F2},{fit.TranslateYDip:F2}");
            lines.Add($"SinglePassMapping={fit.SinglePassMapping}");
            lines.Add($"CapabilitiesFallback={fit.CapabilitiesFallback}");
            lines.Add($"Bitmap={bitmapWidthPx}x{bitmapHeightPx}");
            lines.Add($"EdgeClearance L={leftEdgeClearancePx:F0} R={rightEdgeClearancePx:F0} T={topEdgeClearancePx:F0} B={bottomEdgeClearancePx:F0}");
            lines.Add($"SpoolSubmissionResult={spoolSubmissionResult}");
            File.AppendAllLines(LogPath, lines);
            File.AppendAllText(LogPath, Environment.NewLine);
        }
        catch
        {
            // diagnostics only
        }
    }
}
