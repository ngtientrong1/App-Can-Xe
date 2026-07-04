using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Printing;
using CanXe.Application.Models;
using CanXe.Domain.Services;
using CanXe.Desktop.Views.Printing;

namespace CanXe.Desktop.Services;

public sealed class WeighTicketRasterPrintRenderer
{
    public const double RasterDpi = 300.0;
    public const double MinNonWhitePixelRatio = 0.01;

    public static int A5RasterPixelWidth =>
        (int)Math.Round(WeighTicketPrintLayout.TicketLogicalWidthMm / WeighTicketPrintLayout.MmPerInch * RasterDpi);

    public static int A5RasterPixelHeight =>
        (int)Math.Round(WeighTicketPrintLayout.TicketLogicalHeightMm / WeighTicketPrintLayout.MmPerInch * RasterDpi);

    public static int A4RasterPixelWidth =>
        (int)Math.Round(WeighTicketPrintLayout.PageWidthMm / WeighTicketPrintLayout.MmPerInch * RasterDpi);

    public static int A4RasterPixelHeight =>
        (int)Math.Round(WeighTicketPrintLayout.PageHeightMm / WeighTicketPrintLayout.MmPerInch * RasterDpi);

    [Obsolete("Use A5RasterPixelWidth for A5 single ticket.")]
    public static int RasterPixelWidth => A4RasterPixelWidth;

    [Obsolete("Use A5RasterPixelHeight for A5 single ticket.")]
    public static int RasterPixelHeight => A4RasterPixelHeight;

    public FixedDocument CreateRasterDocument(
        WeighTicketPrintModel model,
        WeighTicketPrintRenderingLogger logger,
        out double nonWhiteRatio) =>
        CreateRasterDocument(model, imageableArea: null, logger, out nonWhiteRatio, out _);

    public FixedDocument CreateRasterDocument(
        WeighTicketPrintModel model,
        PageImageableArea? imageableArea,
        WeighTicketPrintRenderingLogger logger,
        out double nonWhiteRatio) =>
        CreateRasterDocument(model, imageableArea, logger, out nonWhiteRatio, out _);

    public FixedDocument CreateRasterDocument(
        WeighTicketPrintModel model,
        PageImageableArea? imageableArea,
        WeighTicketPrintRenderingLogger logger,
        out double nonWhiteRatio,
        out PrintA5AdaptiveFitOutcome? adaptiveOutcome) =>
        CreateRasterA4TwoUpDocument(model, imageableArea, logger, out nonWhiteRatio, out adaptiveOutcome);

    public FixedDocument CreateRasterDocument(
        WeighTicketPrintModel model,
        PrinterCapabilitiesReader.A5PrintCapabilities a5Caps,
        WeighTicketPrintRenderingLogger logger,
        out double nonWhiteRatio,
        out PrintA5AdaptiveFitOutcome? adaptiveOutcome) =>
        CreateRasterA5SingleDocument(model, a5Caps.Imageable, a5Caps, logger, out nonWhiteRatio, out adaptiveOutcome);

    public FixedDocument CreateRasterA5SingleDocument(
        WeighTicketPrintModel model,
        PageImageableArea? imageableArea,
        WeighTicketPrintRenderingLogger logger,
        out double nonWhiteRatio,
        out PrintA5AdaptiveFitOutcome? adaptiveOutcome) =>
        CreateRasterA5SingleDocument(model, imageableArea, a5Caps: null, logger, out nonWhiteRatio, out adaptiveOutcome);

    public FixedDocument CreateRasterA5SingleDocument(
        WeighTicketPrintModel model,
        PageImageableArea? imageableArea,
        PrinterCapabilitiesReader.A5PrintCapabilities? a5Caps,
        WeighTicketPrintRenderingLogger logger,
        out double nonWhiteRatio,
        out PrintA5AdaptiveFitOutcome? adaptiveOutcome)
    {
        var orientation = a5Caps?.Orientation;
        var pageWidth = orientation?.ActualPageWidthDip ?? WeighTicketPrintLayout.A5LandscapePageWidthDip;
        var pageHeight = orientation?.ActualPageHeightDip ?? WeighTicketPrintLayout.A5LandscapePageHeightDip;
        var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
        var headerReport = PrintA5FitValidator.ValidateHeaderTextBounds(copy);
        logger.LogA5Fit("header-bounds", headerReport.Lines);

        var landscapeBitmap = RenderTicket(copy);
        nonWhiteRatio = ComputeNonWhitePixelRatio(landscapeBitmap);
        logger.LogRaster("pre-spool-a5", landscapeBitmap.PixelWidth, landscapeBitmap.PixelHeight, nonWhiteRatio);

        if (nonWhiteRatio < MinNonWhitePixelRatio)
            throw new InvalidOperationException("Không thể dựng nội dung phiếu để in.");

        var placementBitmap = (BitmapSource)landscapeBitmap;
        var sourceWidthDip = WeighTicketPrintLayout.TicketLogicalWidthDip;
        var sourceHeightDip = WeighTicketPrintLayout.TicketLogicalHeightDip;
        if (orientation?.Mode == A5PhysicalOrientationMode.PortraitDriverFallbackRotateClockwise)
        {
            placementBitmap = A5RasterBitmapRotator.RotateClockwise90(landscapeBitmap);
            sourceWidthDip = orientation.RotatedContentWidthDip;
            sourceHeightDip = orientation.RotatedContentHeightDip;
            logger.LogA5Fit("orientation-rotation", [
                "PhysicalOrientationMode=PortraitDriverFallbackRotateClockwise",
                "RotationDegrees=90",
                $"RotatedBitmap={placementBitmap.PixelWidth}x{placementBitmap.PixelHeight}",
                $"RotatedLogicalSize={sourceWidthDip:F2}x{sourceHeightDip:F2}"
            ]);
        }

        var scanWatermark = model.IsReprint && model.PrintSettings.ShowReprintWatermark;
        var orientationDiagnostics = BuildOrientationDiagnostics(a5Caps);
        PrintA5AdaptiveFitOutcome adaptive;
        if (orientation?.Mode == A5PhysicalOrientationMode.PortraitDriverFallbackRotateClockwise)
        {
            var landscapeBounds = RasterContentBoundsAnalyzer.AnalyzeTicketBitmap(landscapeBitmap, scanWatermark);
            var rawMainRotated = A5PrintBoundsTransformer.RotateBoundsClockwise90(
                landscapeBounds.MainContentBoundsDip.IsEmpty
                    ? ContentBoundsDip.FromSize(
                        WeighTicketPrintLayout.TicketLogicalWidthDip,
                        WeighTicketPrintLayout.TicketLogicalHeightDip)
                    : landscapeBounds.MainContentBoundsDip,
                WeighTicketPrintLayout.TicketLogicalWidthDip,
                WeighTicketPrintLayout.TicketLogicalHeightDip);
            ContentBoundsDip? watermarkRotated = null;
            if (landscapeBounds.WatermarkBoundsDip is { } wm && !wm.IsEmpty)
            {
                watermarkRotated = A5PrintBoundsTransformer.RotateBoundsClockwise90(
                    wm,
                    WeighTicketPrintLayout.TicketLogicalWidthDip,
                    WeighTicketPrintLayout.TicketLogicalHeightDip);
            }

            adaptive = PrintA5AdaptiveFitService.ResolveWithMainBounds(
                placementBitmap,
                imageableArea,
                pageWidth,
                pageHeight,
                sourceWidthDip,
                sourceHeightDip,
                scanWatermark,
                orientation,
                rawMainRotated,
                watermarkRotated);
        }
        else
        {
            adaptive = PrintA5AdaptiveFitService.Resolve(
                placementBitmap,
                imageableArea,
                pageWidth,
                pageHeight,
                sourceWidthDip,
                sourceHeightDip,
                scanWatermark,
                orientation,
                orientationDiagnostics,
                rawMainContentOverride: null);
        }
        adaptiveOutcome = adaptive;
        logger.LogA5Fit("adaptive-placement", adaptive.DiagnosticLines);

        if (!adaptive.Passed)
            throw new InvalidOperationException(PrintA5AdaptiveFitOutcome.ValidationFailureMessage);

        var placement = adaptive.FinalPlacement;
        var document = BuildRasterPage(
            placementBitmap,
            placement,
            pageWidth,
            pageHeight,
            imageableArea,
            logger);

        var rasterPage = document.Pages[0].GetPageRoot(false) as FixedPage;
        if (rasterPage is not null)
        {
            WpfWeighTicketDocumentFactory.Materialize(rasterPage);
            var pageBitmap = RenderPageAtSize(rasterPage, pageWidth, pageHeight);
            var pageReport = PrintA5FitValidator.ValidateA5PageRasterPlacement(pageBitmap, placement, pageWidth, pageHeight);
            logger.LogA5Fit("page-raster-clearance", pageReport.Lines);
            if (!pageReport.Passed)
                logger.LogA5Fit("page-raster-clearance-warning", pageReport.Lines);
        }

        return document;
    }

    private static IReadOnlyList<string>? BuildOrientationDiagnostics(PrinterCapabilitiesReader.A5PrintCapabilities? a5Caps)
    {
        if (a5Caps is null)
            return null;

        var orientation = a5Caps.Orientation;
        return
        [
            $"RequestedOrientation=Landscape",
            $"ValidatedOrientation={a5Caps.ValidatedOrientation}",
            $"ActualPageWidthDip={orientation.ActualPageWidthDip:F2}",
            $"ActualPageHeightDip={orientation.ActualPageHeightDip:F2}",
            $"ActualPageAspect={orientation.ActualPageAspect}",
            $"DriverIgnoredLandscape={orientation.DriverIgnoredLandscape}",
            $"LogicalLandscapeSize={WeighTicketPrintLayout.TicketLogicalWidthMm}x{WeighTicketPrintLayout.TicketLogicalHeightMm}mm",
            "SinglePassMapping=true"
        ];
    }

    public FixedDocument CreateRasterA4TwoUpDocument(
        WeighTicketPrintModel model,
        PageImageableArea? imageableArea,
        WeighTicketPrintRenderingLogger logger,
        out double nonWhiteRatio,
        out PrintA5AdaptiveFitOutcome? adaptiveOutcome)
    {
        adaptiveOutcome = null;
        var vectorDocument = new WpfWeighTicketDocumentFactory().CreateA4TwoUpDocument(model);
        var page = GetFixedPage(vectorDocument);
        WpfWeighTicketDocumentFactory.Materialize(page);

        var bitmap = RenderPage(page);
        nonWhiteRatio = ComputeNonWhitePixelRatio(bitmap);
        logger.LogRaster("pre-spool-a4", bitmap.PixelWidth, bitmap.PixelHeight, nonWhiteRatio);

        if (nonWhiteRatio < MinNonWhitePixelRatio)
            throw new InvalidOperationException("Không thể dựng nội dung phiếu để in.");

        var placement = imageableArea is not null
            ? RasterPrintLayoutMapper.MapWithSafetyInset(
                imageableArea,
                WeighTicketPrintLayout.PageWidthDip,
                WeighTicketPrintLayout.PageHeightDip)
            : RasterPrintLayoutMapper.MapFullPage(
                WeighTicketPrintLayout.PageWidthDip,
                WeighTicketPrintLayout.PageHeightDip);

        if (imageableArea is not null)
        {
            var clearance = PrintLayoutGeometryValidator.ValidateRaster(bitmap, placement);
            logger.LogRasterClearance(clearance);
            if (!clearance.Passed)
                throw new InvalidOperationException("Print-safe raster edge clearance validation failed.");
        }

        return BuildRasterPage(
            bitmap,
            placement,
            WeighTicketPrintLayout.PageWidthDip,
            WeighTicketPrintLayout.PageHeightDip,
            imageableArea,
            logger);
    }

    private static FixedDocument BuildRasterPage(
        BitmapSource bitmap,
        RasterPlacement placement,
        double pageWidthDip,
        double pageHeightDip,
        PageImageableArea? imageableArea,
        WeighTicketPrintRenderingLogger logger)
    {
        var rasterDocument = new FixedDocument();
        var rasterPage = new FixedPage
        {
            Width = pageWidthDip,
            Height = pageHeightDip,
            Background = WeighTicketPrintResources.PageBackground
        };

        var image = new Image
        {
            Source = bitmap,
            Width = placement.WidthDip,
            Height = placement.HeightDip,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true
        };
        FixedPage.SetLeft(image, placement.LeftDip);
        FixedPage.SetTop(image, placement.TopDip);
        rasterPage.Children.Add(image);

        if (imageableArea is not null)
        {
            logger.LogRasterImageableMapping(
                imageableArea.OriginWidth,
                imageableArea.OriginHeight,
                imageableArea.ExtentWidth,
                imageableArea.ExtentHeight,
                placement);
        }
        else
        {
            logger.LogRasterImageableMapping(0, 0, placement.TargetWidthDip, placement.TargetHeightDip, placement);
        }

        var content = new PageContent();
        ((IAddChild)content).AddChild(rasterPage);
        rasterDocument.Pages.Add(content);
        return rasterDocument;
    }

    public static RenderTargetBitmap RenderTicket(WeighTicketCopyView copy)
    {
        var width = A5RasterPixelWidth;
        var height = A5RasterPixelHeight;
        WpfWeighTicketDocumentFactory.MaterializeTicket(copy);

        var surface = new Grid
        {
            Width = WeighTicketPrintLayout.TicketLogicalWidthDip,
            Height = WeighTicketPrintLayout.TicketLogicalHeightDip,
            Background = WeighTicketPrintResources.PageBackground
        };
        surface.Children.Add(copy);
        surface.Measure(new Size(surface.Width, surface.Height));
        surface.Arrange(new Rect(0, 0, surface.Width, surface.Height));
        surface.UpdateLayout();

        var bitmap = new RenderTargetBitmap(width, height, RasterDpi, RasterDpi, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        bitmap.Freeze();
        return bitmap;
    }

    public static int A5PageRasterPixelWidth =>
        (int)Math.Round(WeighTicketPrintLayout.A5LandscapePageWidthMm / WeighTicketPrintLayout.MmPerInch * RasterDpi);

    public static int A5PageRasterPixelHeight =>
        (int)Math.Round(WeighTicketPrintLayout.A5LandscapePageHeightMm / WeighTicketPrintLayout.MmPerInch * RasterDpi);

    public static RenderTargetBitmap RenderA5Page(Visual visual) =>
        RenderPageAtSize(visual, WeighTicketPrintLayout.A5LandscapePageWidthDip, WeighTicketPrintLayout.A5LandscapePageHeightDip);

    public static RenderTargetBitmap RenderPageAtSize(Visual visual, double pageWidthDip, double pageHeightDip)
    {
        var width = (int)Math.Round(pageWidthDip / WeighTicketPrintLayout.Dpi * RasterDpi);
        var height = (int)Math.Round(pageHeightDip / WeighTicketPrintLayout.Dpi * RasterDpi);
        var bitmap = new RenderTargetBitmap(width, height, RasterDpi, RasterDpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public static RenderTargetBitmap RenderPage(Visual visual)
    {
        var width = A4RasterPixelWidth;
        var height = A4RasterPixelHeight;
        var bitmap = new RenderTargetBitmap(width, height, RasterDpi, RasterDpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public static RenderTargetBitmap RenderCopy(WeighTicketCopyView copy) => RenderTicket(copy);

    public static double ComputeNonWhitePixelRatio(BitmapSource bitmap)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        if (width <= 0 || height <= 0)
            return 0;

        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var nonWhite = 0;
        var total = width * height;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            var a = pixels[i + 3];
            if (a > 16 && (r < 245 || g < 245 || b < 245))
                nonWhite++;
        }

        return nonWhite / (double)total;
    }

    private static FixedPage GetFixedPage(FixedDocument document)
    {
        if (document.Pages.Count == 0)
            throw new InvalidOperationException("Document has no pages.");

        if (document.Pages[0].GetPageRoot(false) is FixedPage page)
            return page;

        throw new InvalidOperationException("Expected FixedPage root.");
    }
}
