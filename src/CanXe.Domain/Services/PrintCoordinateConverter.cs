namespace CanXe.Domain.Services;

public static class PrintCoordinateConverter
{
    public static double DipToRasterPx(double dip) =>
        dip * WeighTicketPrintLayout.PrintRasterDpi / WeighTicketPrintLayout.Dpi;

    public static double RasterPxToDip(double px) =>
        px * WeighTicketPrintLayout.Dpi / WeighTicketPrintLayout.PrintRasterDpi;

    public static double MmToDip(double mm) => WeighTicketPrintLayout.MmToDip(mm);

    public static double DipToMm(double dip) => WeighTicketPrintLayout.DipToMm(dip);
}
