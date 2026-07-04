using System.Printing;
using CanXe.Application.Models;
using CanXe.Desktop.Windows;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public static class PrintPreviewScaleResolver
{
    public static PrintPreviewInfo Resolve(PrintLayoutMode layoutMode)
    {
        if (layoutMode == PrintLayoutMode.A4TwoUp)
        {
            return new PrintPreviewInfo
            {
                ModeLabel = "A4 dọc — 2 liên A5 ngang",
                FinalScalePercent = 100
            };
        }

        try
        {
            using var server = new LocalPrintServer();
            var queue = server.DefaultPrintQueue ?? server.GetPrintQueues().FirstOrDefault();
            if (queue is null)
                return DefaultA5();

            var ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket;
            ticket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA5);
            ticket.PageOrientation = PageOrientation.Landscape;

            var caps = PrinterCapabilitiesReader.ReadA5Landscape(queue, ticket);
            var fit = caps.CapabilitiesFallback
                ? PrintA5FitCalculator.ComputeFallback(
                    caps.ExtentWidthDip,
                    caps.ExtentHeightDip,
                    WeighTicketPrintLayout.TicketLogicalWidthDip,
                    WeighTicketPrintLayout.TicketLogicalHeightDip)
                : PrintA5FitCalculator.Compute(
                    caps.OriginWidthDip,
                    caps.OriginHeightDip,
                    caps.ExtentWidthDip,
                    caps.ExtentHeightDip,
                    WeighTicketPrintLayout.TicketLogicalWidthDip,
                    WeighTicketPrintLayout.TicketLogicalHeightDip,
                    caps.CapabilitiesFallback);

            return new PrintPreviewInfo
            {
                ModeLabel = "A5 ngang — Fit to printable area",
                FinalScalePercent = fit.FinalScale * 100
            };
        }
        catch
        {
            return DefaultA5();
        }
    }

    private static PrintPreviewInfo DefaultA5() =>
        new()
        {
            ModeLabel = "A5 ngang — Fit to printable area",
            FinalScalePercent = 100
        };
}
