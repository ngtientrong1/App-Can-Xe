using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

/// <summary>Renders the printed ticket with a single price and with a two-level price to PNG for a visual check.</summary>
public static class TicketPriceSplitPrintTestRunner
{
    public static int Run(string outputDirectory)
    {
        var exitCode = 1;
        var thread = new Thread(() =>
        {
            try
            {
                exitCode = RunOnSta(outputDirectory);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex}");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return exitCode;
    }

    private static int RunOnSta(string outputDirectory)
    {
        if (System.Windows.Application.Current is null)
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        Directory.CreateDirectory(outputDirectory);
        var factory = new WpfWeighTicketDocumentFactory();

        foreach (var (name, split, notes) in new[]
                 {
                     ("single", false, (string?)null),
                     ("split", true, null),
                     ("split-with-notes", true, "Giao hàng buổi sáng, xe đi ngay")
                 })
        {
            var document = factory.CreateA4TwoUpDocument(BuildModel(split, notes));
            var size = document.DocumentPaginator.PageSize;
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(document.Pages[0].GetPageRoot(false));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(outputDirectory, $"ticket-{name}.png");
            using var stream = File.Create(path);
            encoder.Save(stream);
            Console.WriteLine(path);
        }

        return 0;
    }

    private static WeighTicketPrintModel BuildModel(bool split, string? notes)
    {
        var first = 15000m;
        var price1 = 19800m;
        var price2 = 19600m;
        var billable = 16022m;
        var total = split
            ? PriceBreakdown.TotalAmount(PriceBreakdown.Compute(billable, price1, new PriceSplit(first, price2)))
            : billable * price1;

        return WeighTicketPrintModelMapper.FromDetail(
            new WeighTicketDetailDto
            {
                Id = 7,
                DisplayNumber = "61/10",
                CustomerName = "Công Ty Đại Lợi",
                LicensePlate = "82C-072.19",
                CargoTypeName = "Rô Tươi",
                Notes = notes,
                GrossWeightKg = 21070m,
                TareWeightKg = 5000m,
                NetWeightKg = 16070m,
                DeductionWeightKg = 48.21m,
                BillableWeightKg = billable,
                UnitPriceVndPerKg = price1,
                SplitFirstWeightKg = split ? first : null,
                SecondUnitPriceVndPerKg = split ? price2 : null,
                TotalAmountVnd = total,
                Weight1RecordedAt = new DateTimeOffset(2026, 10, 27, 7, 40, 33, TimeSpan.FromHours(7)),
                Weight2RecordedAt = new DateTimeOffset(2026, 10, 27, 7, 46, 7, TimeSpan.FromHours(7)),
                TicketDateTime = new DateTimeOffset(2026, 10, 27, 7, 46, 7, TimeSpan.FromHours(7))
            },
            new StationSettingsDto
            {
                StationName = "TRẠM CÂN TIẾN TRỌNG",
                StationSubtitle = "TRẠM CÂN ĐIỆN TỬ 60 TẤN",
                Phone = "0865407403"
            },
            ProductionPrintLayoutPolicy.NormalizeForProduction(new PrintSettingsDto
            {
                PrintRenderingMode = PrintRenderingMode.RasterCompatibility,
                ShowReprintWatermark = true
            }),
            isReprint: false);
    }
}
