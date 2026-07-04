namespace CanXe.Domain.Services;

public readonly record struct PrintA5FitResult(
    double TargetLeftDip,
    double TargetTopDip,
    double TargetWidthDip,
    double TargetHeightDip,
    double ScaleX,
    double ScaleY,
    double FinalScale,
    double FinalWidthDip,
    double FinalHeightDip,
    double TranslateXDip,
    double TranslateYDip,
    bool SinglePassMapping,
    bool CapabilitiesFallback);

public static class PrintA5FitCalculator
{
    public static PrintA5FitResult Compute(
        double originWidthDip,
        double originHeightDip,
        double extentWidthDip,
        double extentHeightDip,
        double contentWidthDip,
        double contentHeightDip,
        bool capabilitiesFallback = false)
    {
        var insetH = WeighTicketPrintLayout.PrinterImageableSafetyInsetHorizontalDip;
        var insetV = WeighTicketPrintLayout.PrinterImageableSafetyInsetVerticalDip;
        var targetLeft = originWidthDip + insetH;
        var targetTop = originHeightDip + insetV;
        var targetWidth = extentWidthDip - insetH * 2;
        var targetHeight = extentHeightDip - insetV * 2;

        if (targetWidth <= 0 || targetHeight <= 0)
            throw new InvalidOperationException("Vùng in an toàn của máy in không hợp lệ.");

        var scaleX = targetWidth / contentWidthDip;
        var scaleY = targetHeight / contentHeightDip;
        var finalScale = Math.Min(1.0, Math.Min(scaleX, scaleY));
        var finalWidth = contentWidthDip * finalScale;
        var finalHeight = contentHeightDip * finalScale;
        var translateX = targetLeft + (targetWidth - finalWidth) / 2;
        var translateY = targetTop + (targetHeight - finalHeight) / 2;

        return new PrintA5FitResult(
            targetLeft,
            targetTop,
            targetWidth,
            targetHeight,
            scaleX,
            scaleY,
            finalScale,
            finalWidth,
            finalHeight,
            translateX,
            translateY,
            SinglePassMapping: true,
            capabilitiesFallback);
    }

    public static PrintA5FitResult ComputeFallback(
        double pageWidthDip,
        double pageHeightDip,
        double contentWidthDip,
        double contentHeightDip) =>
        Compute(0, 0, pageWidthDip, pageHeightDip, contentWidthDip, contentHeightDip, capabilitiesFallback: true);
}
