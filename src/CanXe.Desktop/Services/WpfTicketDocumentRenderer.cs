using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class WpfTicketDocumentRenderer : ITicketDocumentRenderer
{
    public TicketDocumentRenderResult RenderFront(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null) =>
        Render(detail, options, includeFront: true, includeBack: false);

    public TicketDocumentRenderResult RenderBack(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null) =>
        Render(detail, options, includeFront: false, includeBack: true);

    public TicketDocumentRenderResult RenderCombinedVertical(WeighTicketDetailDto detail, TicketDocumentRenderOptions? options = null) =>
        Render(detail, options, includeFront: true, includeBack: true);

    private static TicketDocumentRenderResult Render(
        WeighTicketDetailDto detail,
        TicketDocumentRenderOptions? options,
        bool includeFront,
        bool includeBack)
    {
        options ??= new TicketDocumentRenderOptions();
        var width = options.WidthPx;
        var height = (includeFront ? options.FrontHeightPx : 0) + (includeBack ? options.BackHeightPx : 0);
        if (height == 0)
            height = options.FrontHeightPx;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var y = 24.0;

            if (includeFront)
            {
                y = DrawFront(dc, detail, options, width, y);
                y += 24;
            }

            if (includeBack)
                DrawBack(dc, detail, width, y, options.BackHeightPx);
        }

        var png = RenderToPng(visual, width, height);
        var fileName = BuildFileName(detail, includeFront && includeBack ? "combined" : includeBack ? "back" : "front");
        return new TicketDocumentRenderResult { PngBytes = png, SuggestedFileName = fileName };
    }

    private static double DrawFront(
        DrawingContext dc,
        WeighTicketDetailDto detail,
        TicketDocumentRenderOptions options,
        int width,
        double y)
    {
        var title = new Typeface("Segoe UI");
        dc.DrawText(MakeText(options.ScaleSiteName, 22, FontWeights.Bold, title), new Point(24, y));
        y += 28;
        if (!string.IsNullOrWhiteSpace(options.OwnerName))
        {
            dc.DrawText(MakeText(options.OwnerName, 15, FontWeights.Normal, title), new Point(24, y));
            y += 20;
        }

        if (!string.IsNullOrWhiteSpace(options.Address))
        {
            dc.DrawText(MakeText(options.Address, 14, FontWeights.Normal, title), new Point(24, y));
            y += 18;
        }

        var contactParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.Phone))
            contactParts.Add($"ĐT: {options.Phone}");
        if (!string.IsNullOrWhiteSpace(options.Email))
            contactParts.Add(options.Email);
        if (contactParts.Count > 0)
        {
            dc.DrawText(MakeText(string.Join("  ·  ", contactParts), 14, FontWeights.Normal, title), new Point(24, y));
            y += 20;
        }

        y += 8;
        dc.DrawText(MakeText($"PHIẾU CÂN XE {detail.DisplayNumber}", 20, FontWeights.Bold, title), new Point(24, y));
        y += 32;
        dc.DrawText(MakeText(detail.TicketDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture), 16, FontWeights.Normal, title),
            new Point(24, y));
        y += 28;

        y = DrawLine(dc, title, "Khách hàng", detail.CustomerName ?? "—", y);
        y = DrawLine(dc, title, "Biển số", detail.LicensePlate ?? "—", y);
        y = DrawLine(dc, title, "Loại hàng", detail.CargoTypeName ?? "—", y);
        y = DrawLine(dc, title, "Cân lần 1", FormatWeight(detail.Weight1Kg, detail.Weight1RecordedAt), y);
        y = DrawLine(dc, title, "Cân lần 2", FormatWeight(detail.Weight2Kg, detail.Weight2RecordedAt), y);
        y = DrawLine(dc, title, "Tổng", FormatKg(detail.GrossWeightKg), y);
        y = DrawLine(dc, title, "Bì", FormatKg(detail.TareWeightKg), y);
        y = DrawLine(dc, title, "Hàng", FormatKg(detail.NetWeightKg), y);

        if (WeightCalculator.HasBillableUnitPrice(detail.UnitPriceVndPerKg))
        {
            y = DrawLine(dc, title, "Trừ bì", detail.DeductionWeightKg?.ToString("N3", CultureInfo.CurrentCulture) ?? "—", y);
            y = DrawLine(dc, title, "KL tính tiền", FormatKg(detail.BillableWeightKg), y);
            y = DrawLine(dc, title, "Đơn giá", detail.UnitPriceVndPerKg?.ToString("N0", CultureInfo.CurrentCulture) + " VNĐ/kg", y);
            y = DrawLine(dc, title, "Thành tiền", detail.TotalAmountVnd?.ToString("N0", CultureInfo.CurrentCulture) + " VNĐ", y);
        }
        else
        {
            y = DrawLine(dc, title, "Loại phiếu", "Cân dịch vụ", y);
        }

        y = DrawLine(dc, title, "Ghi chú", detail.Notes ?? "—", y);
        if (!string.IsNullOrWhiteSpace(options.TicketFooterText))
        {
            y += 8;
            dc.DrawText(MakeText(options.TicketFooterText, 13, FontWeights.Normal, title), new Point(24, y));
            y += 18;
        }

        y += 12;
        dc.DrawText(MakeText("Người cân .................    Khách hàng .................", 14, FontWeights.Normal, title),
            new Point(24, y));
        return y + 24;
    }

    private static void DrawBack(DrawingContext dc, WeighTicketDetailDto detail, int width, double y, int sectionHeight)
    {
        var title = new Typeface("Segoe UI");
        dc.DrawText(MakeText("ẢNH CÂN", 20, FontWeights.Bold, title), new Point(24, y));
        y += 32;
        DrawPhotoSection(dc, title, "Cân lần 1", detail.Weight1PhotoPath, detail.Weight1PhotoAvailable, detail.Weight1PhotoStatusText, 24, y, width - 48);
        y += sectionHeight / 2 - 16;
        DrawPhotoSection(dc, title, "Cân lần 2", detail.Weight2PhotoPath, detail.Weight2PhotoAvailable, detail.Weight2PhotoStatusText, 24, y, width - 48);
    }

    private static void DrawPhotoSection(
        DrawingContext dc,
        Typeface title,
        string label,
        string? path,
        bool available,
        string? statusText,
        double x,
        double y,
        double width)
    {
        dc.DrawText(MakeText(label, 16, FontWeights.SemiBold, title), new Point(x, y));
        y += 22;
        var rect = new Rect(x, y, width, 180);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 245, 245)), new Pen(Brushes.Gray, 1), rect);

        if (available && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            dc.DrawImage(bitmap, rect);
        }
        else
        {
            var message = statusText ?? "Chưa có ảnh cân";
            dc.DrawText(MakeText(message, 14, FontWeights.Normal, title), new Point(x + 12, y + 80));
        }
    }

    private static double DrawLine(DrawingContext dc, Typeface title, string label, string value, double y)
    {
        dc.DrawText(MakeText($"{label}: {value}", 15, FontWeights.Normal, title), new Point(24, y));
        return y + 22;
    }

    private static FormattedText MakeText(string text, double size, FontWeight weight, Typeface typeface) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, Brushes.Black, 1.25);

    private static string FormatKg(decimal? kg) =>
        kg?.ToString("N0", CultureInfo.CurrentCulture) + " kg" ?? "—";

    private static string FormatWeight(decimal? kg, DateTimeOffset? at)
    {
        if (!kg.HasValue)
            return "—";

        var time = at?.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        return time is null ? $"{kg.Value:N0} kg" : $"{kg.Value:N0} kg ({time})";
    }

    private static byte[] RenderToPng(DrawingVisual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static string BuildFileName(WeighTicketDetailDto detail, string suffix)
    {
        var plate = (detail.LicensePlate ?? "NOPLATE").Replace("-", "").Replace(".", "");
        var date = detail.TicketDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var number = detail.DisplayNumber.Replace("/", "-");
        return $"{number}_{plate}_{date}_{suffix}.png";
    }
}
