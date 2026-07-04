using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Desktop.Views.Printing;

namespace CanXe.Desktop.Services;

public static class PrintLayoutGeometryTestRunner
{
    public static int Run()
    {
        var exitCode = 1;
        var thread = new Thread(() =>
        {
            try
            {
                exitCode = RunOnSta();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                exitCode = 1;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return exitCode;
    }

    private static int RunOnSta()
    {
        if (System.Windows.Application.Current is null)
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        var model = BuildSampleModelPublic();
        var a4Model = BuildSampleModelPublic(PrintLayoutMode.A4TwoUp);

        var copy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
        var layoutReport = PrintLayoutGeometryValidator.ValidateMaterializedCopy(copy, "Single copy");
        foreach (var line in layoutReport.Lines)
            Console.WriteLine(line);

        var page = new WpfWeighTicketDocumentFactory().CreateA4TwoUpDocument(a4Model);
        var fixedPage = page.Pages[0].GetPageRoot(false) as FixedPage;
        WpfWeighTicketDocumentFactory.Materialize(fixedPage!);

        var copies = fixedPage!.Children.OfType<WeighTicketCopyView>().OrderBy(c => FixedPage.GetTop(c)).ToList();
        var topCopy = copies[0];
        var bottomCopy = copies[1];

        WpfWeighTicketDocumentFactory.Materialize(topCopy);
        WpfWeighTicketDocumentFactory.Materialize(bottomCopy);

        var topReport = PrintLayoutGeometryValidator.ValidateMaterializedCopy(topCopy, "Top A5 copy");
        foreach (var line in topReport.Lines)
            Console.WriteLine(line);

        var bottomReport = PrintLayoutGeometryValidator.ValidateMaterializedCopy(bottomCopy, "Bottom A5 copy");
        foreach (var line in bottomReport.Lines)
            Console.WriteLine(line);

        var bitmap = WeighTicketRasterPrintRenderer.RenderTicket(copy);
        var placement = RasterPrintLayoutMapper.MapFullPage(
            WeighTicketPrintLayout.TicketLogicalWidthDip,
            WeighTicketPrintLayout.TicketLogicalHeightDip);
        var rasterReport = PrintLayoutGeometryValidator.ValidateRasterA5Ticket(bitmap, placement);
        Console.WriteLine("---");
        foreach (var line in rasterReport.Lines)
            Console.WriteLine(line);

        var topRasterCopy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
        var bottomRasterCopy = WpfWeighTicketDocumentFactory.CreateMaterializedCopy(model);
        var topRasterCards = ValidateRasterCardEdgesForCopy(topRasterCopy, "Top A5 copy");
        foreach (var line in topRasterCards.Lines)
            Console.WriteLine(line);

        var bottomRasterCards = ValidateRasterCardEdgesForCopy(bottomRasterCopy, "Bottom A5 copy");
        foreach (var line in bottomRasterCards.Lines)
            Console.WriteLine(line);

        System.Windows.Application.Current?.Shutdown();
        var passed = layoutReport.Passed
                     && topReport.Passed
                     && bottomReport.Passed
                     && rasterReport.Passed
                     && topRasterCards.Passed
                     && bottomRasterCards.Passed;
        return passed ? 0 : 1;
    }

    private static PrintLayoutGeometryReport ValidateRasterCardEdgesForCopy(WeighTicketCopyView copy, string label)
    {
        WpfWeighTicketDocumentFactory.Materialize(copy);
        var bitmap = WeighTicketRasterPrintRenderer.RenderCopy(copy);
        return PrintLayoutGeometryValidator.ValidateRasterCardEdges(bitmap, copy, label);
    }

    private static WeighTicketPrintModel BuildSampleModel() => BuildSampleModelPublic();

    public static WeighTicketPrintModel BuildSampleModelPublic(PrintLayoutMode layoutMode = PrintLayoutMode.A4TwoUp) =>
        WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 1,
                DisplayNumber = "15/06",
                CustomerName = "Khách Hàng C",
                LicensePlate = "81C1234356",
                CargoTypeName = "Rổ nhãn",
                GrossWeightKg = 180m,
                TareWeightKg = 170m,
                NetWeightKg = 10m,
                UnitPriceVndPerKg = 80_000m,
                TotalAmountVnd = 800_000m,
                Weight1RecordedAt = new DateTimeOffset(2026, 6, 29, 12, 45, 33, TimeSpan.FromHours(7)),
                Weight2RecordedAt = new DateTimeOffset(2026, 6, 29, 12, 46, 7, TimeSpan.FromHours(7)),
                TicketDateTime = new DateTimeOffset(2026, 6, 29, 12, 46, 7, TimeSpan.FromHours(7))
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
                Phone = "0865407403"
            },
            new PrintSettingsDto
            {
                PrintRenderingMode = PrintRenderingMode.RasterCompatibility,
                PrintLayoutMode = layoutMode
            },
            isReprint: false);
}
