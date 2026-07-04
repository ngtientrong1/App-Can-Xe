using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

internal sealed class WpfDocumentPageSource(FixedDocument document, PrintLayoutMode layoutMode) : IDocumentPageSource
{
    public double PageWidthDip => layoutMode == PrintLayoutMode.A4TwoUp
        ? WeighTicketPrintLayout.PageWidthDip
        : WeighTicketPrintLayout.A5LandscapePageWidthDip;

    public double PageHeightDip => layoutMode == PrintLayoutMode.A4TwoUp
        ? WeighTicketPrintLayout.PageHeightDip
        : WeighTicketPrintLayout.A5LandscapePageHeightDip;

    public int PageCount => document.Pages.Count;
    public object GetPage(int pageIndex) => document;
    public FixedDocument Document => document;
}

public sealed class ImageableAreaDocumentPaginator : DocumentPaginator
{
    private readonly DocumentPaginator _inner;
    private readonly Size _pageSize;

    public double ScaleFactor { get; }
    public double OffsetXDip { get; }
    public double OffsetYDip { get; }

    public ImageableAreaDocumentPaginator(
        DocumentPaginator inner,
        PageImageableArea imageable,
        double pageWidthDip,
        double pageHeightDip,
        out double scaleFactor)
    {
        _inner = inner;
        _pageSize = new Size(pageWidthDip, pageHeightDip);
        var fit = PrintA5FitCalculator.Compute(
            imageable.OriginWidth,
            imageable.OriginHeight,
            imageable.ExtentWidth,
            imageable.ExtentHeight,
            pageWidthDip,
            pageHeightDip);
        ScaleFactor = scaleFactor = fit.FinalScale;
        OffsetXDip = fit.TranslateXDip;
        OffsetYDip = fit.TranslateYDip;
        _inner.PageSize = _pageSize;
    }

    public override DocumentPage GetPage(int pageNumber)
    {
        var sourcePage = _inner.GetPage(pageNumber);
        var container = new ContainerVisual();
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(ScaleFactor, ScaleFactor));
        group.Children.Add(new TranslateTransform(OffsetXDip, OffsetYDip));
        container.Transform = group;
        container.Children.Add(sourcePage.Visual);
        return new DocumentPage(
            container,
            _pageSize,
            new Rect(OffsetXDip, OffsetYDip, _pageSize.Width * ScaleFactor, _pageSize.Height * ScaleFactor),
            new Rect(_pageSize));
    }

    public override bool IsPageCountValid => _inner.IsPageCountValid;
    public override int PageCount => _inner.PageCount;
    public override Size PageSize
    {
        get => _pageSize;
        set => _inner.PageSize = value;
    }
    public override IDocumentPaginatorSource Source => _inner.Source;
}

public sealed class PrinterCapabilityService : IPrinterCapabilityService
{
    public IReadOnlyList<PrinterInfo> GetInstalledPrinters()
    {
        using var server = new LocalPrintServer();
        return server.GetPrintQueues()
            .Select(q => new PrinterInfo
            {
                Name = q.Name,
                IsDefault = q.Name == server.DefaultPrintQueue?.Name,
                SupportsA4 = true,
                IsOnline = !q.IsOffline
            })
            .ToList();
    }

    public PrinterInfo? GetDefaultPrinter()
    {
        using var server = new LocalPrintServer();
        var queue = server.DefaultPrintQueue;
        return queue is null ? null : new PrinterInfo
        {
            Name = queue.Name,
            IsDefault = true,
            SupportsA4 = true,
            IsOnline = !queue.IsOffline
        };
    }

    public PrinterValidationResult ValidatePrinter(string? preferredPrinterName)
    {
        using var server = new LocalPrintServer();
        var name = preferredPrinterName ?? server.DefaultPrintQueue?.Name;
        if (string.IsNullOrWhiteSpace(name))
            return PrinterValidationResult.Fail("Không tìm thấy máy in khả dụng.");

        var queue = server.GetPrintQueues().FirstOrDefault(q =>
            q.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (queue is null)
            return PrinterValidationResult.Fail($"Không tìm thấy máy in: {name}");

        return PrinterValidationResult.Ok(new PrinterInfo
        {
            Name = queue.Name,
            IsDefault = queue.Name == server.DefaultPrintQueue?.Name,
            SupportsA4 = true,
            IsOnline = !queue.IsOffline
        });
    }
}

public static class PrinterCapabilitiesReader
{
    public sealed record A5PrintCapabilities(
        PrintCapabilities Capabilities,
        PageImageableArea? Imageable,
        bool CapabilitiesFallback,
        double PageWidthDip,
        double PageHeightDip,
        double OriginWidthDip,
        double OriginHeightDip,
        double ExtentWidthDip,
        double ExtentHeightDip,
        A5PhysicalOrientationResolution Orientation,
        PageOrientation? ValidatedOrientation);

    [Obsolete("Use ReadA5Print.")]
    public sealed record A5LandscapeCapabilities(
        PrintCapabilities Capabilities,
        PageImageableArea? Imageable,
        bool CapabilitiesFallback,
        double PageWidthDip,
        double PageHeightDip,
        double OriginWidthDip,
        double OriginHeightDip,
        double ExtentWidthDip,
        double ExtentHeightDip);

    public static A5PrintCapabilities ReadA5Print(
        PrintQueue queue,
        PrintTicket validatedTicket,
        PageOrientation requestedOrientation)
    {
        var capabilities = queue.GetPrintCapabilities(validatedTicket);
        var imageable = capabilities.PageImageableArea;
        var pageWidth = capabilities.OrientedPageMediaWidth ?? WeighTicketPrintLayout.A5LandscapePageWidthDip;
        var pageHeight = capabilities.OrientedPageMediaHeight ?? WeighTicketPrintLayout.A5LandscapePageHeightDip;
        var validatedOrientation = validatedTicket.PageOrientation;
        var orientation = A5PhysicalOrientationResolver.Resolve(
            pageWidth,
            pageHeight,
            ToOrientationLabel(requestedOrientation),
            validatedOrientation is null ? null : ToOrientationLabel(validatedOrientation.Value));

        if (imageable is not null &&
            imageable.ExtentWidth > 0 &&
            imageable.ExtentHeight > 0)
        {
            return new A5PrintCapabilities(
                capabilities,
                imageable,
                false,
                pageWidth,
                pageHeight,
                imageable.OriginWidth,
                imageable.OriginHeight,
                imageable.ExtentWidth,
                imageable.ExtentHeight,
                orientation,
                validatedOrientation);
        }

        return new A5PrintCapabilities(
            capabilities,
            null,
            true,
            pageWidth,
            pageHeight,
            0,
            0,
            pageWidth,
            pageHeight,
            orientation,
            validatedOrientation);
    }

    public static A5LandscapeCapabilities ReadA5Landscape(PrintQueue queue, PrintTicket ticket)
    {
        var caps = ReadA5Print(queue, ticket, PageOrientation.Landscape);
        return new A5LandscapeCapabilities(
            caps.Capabilities,
            caps.Imageable,
            caps.CapabilitiesFallback,
            caps.PageWidthDip,
            caps.PageHeightDip,
            caps.OriginWidthDip,
            caps.OriginHeightDip,
            caps.ExtentWidthDip,
            caps.ExtentHeightDip);
    }

    private static PageOrientationLabel ToOrientationLabel(PageOrientation orientation) =>
        orientation switch
        {
            PageOrientation.Landscape => PageOrientationLabel.Landscape,
            PageOrientation.Portrait => PageOrientationLabel.Portrait,
            _ => PageOrientationLabel.Unknown
        };
}
