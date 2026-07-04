using System.Printing;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public readonly record struct RasterPlacement(
    double LeftDip,
    double TopDip,
    double WidthDip,
    double HeightDip,
    double ScaleFactor,
    double TargetLeftDip,
    double TargetTopDip,
    double TargetWidthDip,
    double TargetHeightDip,
    bool SinglePassMapping);

public static class RasterPrintLayoutMapper
{
    public static RasterPlacement MapFullPage(double pageWidthDip, double pageHeightDip) =>
        FromFitResult(PrintA5FitCalculator.ComputeFallback(
            pageWidthDip,
            pageHeightDip,
            pageWidthDip,
            pageHeightDip));

    public static RasterPlacement MapWithSafetyInset(
        PageImageableArea imageable,
        double sourceWidthDip,
        double sourceHeightDip) =>
        FromFitResult(PrintA5FitCalculator.Compute(
            imageable.OriginWidth,
            imageable.OriginHeight,
            imageable.ExtentWidth,
            imageable.ExtentHeight,
            sourceWidthDip,
            sourceHeightDip));

    public static RasterPlacement MapWithSafetyInsetFromExtents(
        double originWidth,
        double originHeight,
        double extentWidth,
        double extentHeight,
        double sourceWidthDip,
        double sourceHeightDip,
        bool capabilitiesFallback = false) =>
        FromFitResult(PrintA5FitCalculator.Compute(
            originWidth,
            originHeight,
            extentWidth,
            extentHeight,
            sourceWidthDip,
            sourceHeightDip,
            capabilitiesFallback));

    public static RasterPlacement FromFitResult(PrintA5FitResult fit) =>
        new(
            fit.TranslateXDip,
            fit.TranslateYDip,
            fit.FinalWidthDip,
            fit.FinalHeightDip,
            fit.FinalScale,
            fit.TargetLeftDip,
            fit.TargetTopDip,
            fit.TargetWidthDip,
            fit.TargetHeightDip,
            fit.SinglePassMapping);
}
