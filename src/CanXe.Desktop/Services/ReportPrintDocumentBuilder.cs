using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using CanXe.Application.Models;

namespace CanXe.Desktop.Services;

public sealed class ReportPrintModel
{
    public required StationSettingsDto Station { get; init; }
    public required string Period { get; init; }
    public required string Criteria { get; init; }
    public required IReadOnlyList<ReportRowDto> Rows { get; init; }
    public required ReportSummaryDto Summary { get; init; }
    public required int DistinctVehicles { get; init; }
    public DateTime PrintedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// Lays a filtered ticket report out as large, edge-to-edge A4 pages for easy reading: few rows per
/// page, big centered text, a dark header band and soft row striping. Pages are designed landscape
/// and, when <c>rotateToPortrait</c> is set, turned 90 degrees onto a portrait sheet so the printer
/// feeds the paper in its normal direction; the reader turns the sheet. The first page carries the
/// station header and filter description, every page repeats the table header, and the totals
/// (number of trips and vehicles, weights, amount) and signatures follow the last row.
/// </summary>
public static class ReportPrintDocumentBuilder
{
    private static readonly CultureInfo Vn = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly FontFamily Font = new("Segoe UI");

    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x12, 0x1C, 0x2B));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x1F, 0x3A, 0x5F));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x4A, 0x55, 0x68));
    private static readonly Brush Stripe = new SolidColorBrush(Color.FromRgb(0xEE, 0xF3, 0xF9));
    private static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(0xC9, 0xD3, 0xE0));

    // Real printers cannot print to the paper edge (typically 4-6 mm of hardware margin) and the
    // printable area's origin is not always honoured, so keep about 8 mm of clear border.
    private const double PageMargin = 30;
    private const double FooterReserve = 26;

    // Tried in order; the first (size, wrap) at which every column's widest text fits is used. Single-line
    // rows at the two largest sizes come first, then two-line rows (long text wraps) at large sizes, and
    // only then smaller single-line text.
    private static readonly (double Size, bool Wrap)[] LayoutCandidates =
    [
        (22, false), (20, false),
        (22, true), (20, true), (19, true), (18, true),
        (19, false), (18, false),
        (17, true), (16, true), (16, false), (15, true), (14, true), (14, false)
    ];

    private sealed record Column(string Header, Func<ReportRowDto, string> Cell, bool Wrappable = false);

    private sealed record TableLayout(
        double FontSize, double HeaderFontSize, double RowHeight, double HeaderHeight, bool Wrap, double[] Widths);

    public static FixedDocument Build(ReportPrintModel model, double pageWidth, double pageHeight, bool rotateToPortrait = false)
    {
        var columns = BuildColumns(model.Rows);
        var contentWidth = pageWidth - 2 * PageMargin;
        var layout = ComputeLayout(columns, model.Rows, contentWidth);
        var contentHeight = pageHeight - 2 * PageMargin;
        var available = contentHeight - FooterReserve;

        var firstHeaderHeight = MeasureHeight(BuildFirstHeader(model), contentWidth);
        var nextHeaderHeight = MeasureHeight(BuildContinuationHeader(model), contentWidth);
        var totalsHeight = MeasureHeight(BuildTotals(model), contentWidth);

        var firstCapacity = Math.Max(1, (int)((available - firstHeaderHeight - layout.HeaderHeight) / layout.RowHeight));
        var nextCapacity = Math.Max(1, (int)((available - nextHeaderHeight - layout.HeaderHeight) / layout.RowHeight));

        var pages = new List<(int Start, int Count)>();
        var index = 0;
        var capacity = firstCapacity;
        while (index < model.Rows.Count)
        {
            var take = Math.Min(capacity, model.Rows.Count - index);
            pages.Add((index, take));
            index += take;
            capacity = nextCapacity;
        }

        if (pages.Count == 0)
            pages.Add((0, 0));

        var lastHeader = pages.Count == 1 ? firstHeaderHeight : nextHeaderHeight;
        var usedOnLast = lastHeader + layout.HeaderHeight + pages[^1].Count * layout.RowHeight;
        var totalsOnOwnPage = usedOnLast + 12 + totalsHeight > available;
        var totalPages = pages.Count + (totalsOnOwnPage ? 1 : 0);

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = rotateToPortrait
            ? new Size(pageHeight, pageWidth)
            : new Size(pageWidth, pageHeight);

        for (var p = 0; p < pages.Count; p++)
        {
            var body = new StackPanel();
            body.Children.Add(p == 0 ? BuildFirstHeader(model) : BuildContinuationHeader(model));
            body.Children.Add(BuildHeaderRow(columns, layout, contentWidth));

            var (start, count) = pages[p];
            for (var i = start; i < start + count; i++)
                body.Children.Add(BuildDataRow(columns, layout, model.Rows[i], contentWidth, striped: (i - start) % 2 == 1));

            if (p == pages.Count - 1 && !totalsOnOwnPage)
            {
                var totals = BuildTotals(model);
                totals.Margin = new Thickness(0, 12, 0, 0);
                body.Children.Add(totals);
            }

            document.Pages.Add(CreatePage(body, p + 1, totalPages, pageWidth, pageHeight, rotateToPortrait));
        }

        if (totalsOnOwnPage)
        {
            var body = new StackPanel();
            body.Children.Add(BuildContinuationHeader(model));
            body.Children.Add(BuildTotals(model));
            document.Pages.Add(CreatePage(body, totalPages, totalPages, pageWidth, pageHeight, rotateToPortrait));
        }

        return document;
    }

    /// <summary>Renders every page of <paramref name="document"/> to a bitmap (for on-screen preview).</summary>
    public static List<System.Windows.Media.Imaging.BitmapSource> RenderPages(FixedDocument document, double scale)
    {
        var size = document.DocumentPaginator.PageSize;
        var pixelWidth = (int)Math.Ceiling(size.Width * scale);
        var pixelHeight = (int)Math.Ceiling(size.Height * scale);

        var images = new List<System.Windows.Media.Imaging.BitmapSource>();
        foreach (var pageContent in document.Pages)
        {
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(pageContent.GetPageRoot(false));
            bitmap.Freeze();
            images.Add(bitmap);
        }

        return images;
    }

    private static List<Column> BuildColumns(IReadOnlyList<ReportRowDto> rows)
    {
        // Fewer columns means bigger text. The customer is already in the title when the report is for
        // one customer, and "KL tính tiền" only adds information when it differs from the net weight.
        var singleCustomer = rows
            .Select(r => r.CustomerName?.Trim() ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() <= 1;
        var billableDiffers = rows.Any(r => r.BillableWeightKg != r.NetWeightKg);

        var columns = new List<Column>
        {
            new("Ngày giờ", r => r.TicketDateTime.ToString("dd/MM/yy HH:mm", Vn), Wrappable: true),
            new("Số phiếu", r => r.DisplayNumber)
        };

        if (!singleCustomer)
            columns.Add(new("Khách hàng", r => r.CustomerName ?? "—", Wrappable: true));

        columns.Add(new("Biển số", r => r.LicensePlate ?? "—"));
        columns.Add(new("Loại hàng", r => r.CargoTypeName ?? "—", Wrappable: true));
        columns.Add(new("Tổng", r => Number(r.GrossWeightKg)));
        columns.Add(new("Bì", r => Number(r.TareWeightKg)));
        columns.Add(new("Hàng", r => Number(r.NetWeightKg)));

        if (billableDiffers)
            columns.Add(new("KL tính tiền", r => Number(r.BillableWeightKg)));

        columns.Add(new("Đơn giá", r => Number(r.UnitPriceVndPerKg)));
        columns.Add(new("Thành tiền", r => Number(r.TotalAmountVnd)));
        return columns;
    }

    /// <summary>
    /// Picks the largest text size at which every column fits, and sizes the columns in proportion to
    /// what they need. Long values (date, customer, cargo) may wrap onto a second line instead of
    /// shrinking the whole table, so crowded reports stay large and nothing is cut off with an ellipsis.
    /// </summary>
    private static TableLayout ComputeLayout(IReadOnlyList<Column> columns, IReadOnlyList<ReportRowDto> rows, double width)
    {
        const double cellPadding = 18;
        const double wrapShare = 0.62;

        // The widest strings decide each column; checking the longest few keeps this fast on big reports.
        var samples = columns
            .Select(c => rows.Select(c.Cell).Distinct().OrderByDescending(s => s.Length).Take(30).ToArray())
            .ToArray();

        TableLayout? last = null;
        foreach (var (size, wrap) in LayoutCandidates)
        {
            var headerSize = size - 2;
            var needed = new double[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                var widest = 0.0;
                var widestWord = 0.0;
                foreach (var text in samples[i])
                {
                    widest = Math.Max(widest, MeasureText(text, size, FontWeights.SemiBold));
                    if (wrap && columns[i].Wrappable)
                    {
                        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                            widestWord = Math.Max(widestWord, MeasureText(word, size, FontWeights.SemiBold));
                    }
                }

                if (wrap && columns[i].Wrappable)
                    widest = Math.Max(widestWord, widest * wrapShare);

                needed[i] = Math.Max(widest, MeasureText(columns[i].Header, headerSize, FontWeights.Bold)) + cellPadding;
            }

            var total = needed.Sum();
            var scale = width / total;
            last = new TableLayout(
                size,
                headerSize,
                Math.Round(size * (wrap ? 3.1 : 2.2)),
                Math.Round(size * 2.3),
                wrap,
                needed.Select(n => n * scale).ToArray());

            if (total <= width)
                return last;
        }

        return last!;
    }
    private static double MeasureText(string text, double size, FontWeight weight) =>
        new FormattedText(
            text,
            Vn,
            FlowDirection.LeftToRight,
            new Typeface(Font, FontStyles.Normal, weight, FontStretches.Normal),
            size,
            Brushes.Black,
            1.0).Width;

    private static PageContent CreatePage(
        UIElement body, int number, int total, double width, double height, bool rotateToPortrait)
    {
        var grid = new Grid
        {
            Width = width - 2 * PageMargin,
            Height = height - 2 * PageMargin,
            Margin = new Thickness(PageMargin)
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(body, 0);
        grid.Children.Add(body);

        if (total > 1)
        {
            var footer = Text($"Trang {number}/{total}", 16, FontWeights.SemiBold, TextAlignment.Right, Muted);
            Grid.SetRow(footer, 1);
            grid.Children.Add(footer);
        }

        // The landscape design sits on a host the size of the landscape page; for a portrait sheet the
        // host is turned 90 degrees clockwise (its top edge ends up on the right) and moved back on-sheet.
        var host = new Grid { Width = width, Height = height };
        host.Children.Add(grid);

        var sheetWidth = rotateToPortrait ? height : width;
        var sheetHeight = rotateToPortrait ? width : height;
        if (rotateToPortrait)
        {
            host.RenderTransform = new TransformGroup
            {
                Children = { new RotateTransform(90), new TranslateTransform(height, 0) }
            };
        }

        var page = new FixedPage { Width = sheetWidth, Height = sheetHeight, Background = Brushes.White };
        page.Children.Add(host);

        page.Measure(new Size(sheetWidth, sheetHeight));
        page.Arrange(new Rect(0, 0, sheetWidth, sheetHeight));
        page.UpdateLayout();

        var content = new PageContent();
        ((IAddChild)content).AddChild(page);
        return content;
    }

    private static StackPanel BuildFirstHeader(ReportPrintModel model)
    {
        // Compact and spread across the full width: station on the left, report title on the right,
        // then one band with the criteria on the left and the period on the right.
        var station = model.Station;
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(Text(station.StationName.ToUpperInvariant(), 22, FontWeights.Bold, TextAlignment.Left, Accent));
        var contact = string.Join("  -  ", new[]
        {
            station.StationSubtitle,
            station.Address,
            string.IsNullOrWhiteSpace(station.Phone) ? null : $"ĐT: {station.Phone}"
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (contact.Length > 0)
        {
            var contactText = Text(contact, 16, FontWeights.Normal, TextAlignment.Left, Muted);
            contactText.TextTrimming = TextTrimming.CharacterEllipsis;
            left.Children.Add(contactText);
        }

        var title = Text("BÁO CÁO PHIẾU CÂN", 34, FontWeights.ExtraBold, TextAlignment.Right, Ink);
        title.VerticalAlignment = VerticalAlignment.Center;
        title.Margin = new Thickness(16, 0, 0, 0);
        Grid.SetColumn(title, 1);
        top.Children.Add(left);
        top.Children.Add(title);
        panel.Children.Add(top);

        var band = new Grid();
        band.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        band.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var criteria = Text(model.Criteria, 24, FontWeights.Bold, TextAlignment.Left, Accent);
        criteria.TextTrimming = TextTrimming.CharacterEllipsis;
        var period = Text(model.Period, 22, FontWeights.SemiBold, TextAlignment.Right, Ink);
        period.Margin = new Thickness(16, 0, 0, 0);
        Grid.SetColumn(period, 1);
        band.Children.Add(criteria);
        band.Children.Add(period);
        panel.Children.Add(new Border
        {
            Child = band,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(8),
            Background = Stripe,
            BorderBrush = Hairline,
            BorderThickness = new Thickness(1)
        });
        return panel;
    }

    private static TextBlock BuildContinuationHeader(ReportPrintModel model)
    {
        var text = Text(
            $"BÁO CÁO PHIẾU CÂN  -  {model.Criteria}  -  {model.Period}",
            18, FontWeights.Bold, TextAlignment.Left, Accent);
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.Margin = new Thickness(0, 0, 0, 8);
        return text;
    }

    private static StackPanel BuildTotals(ReportPrintModel model)
    {
        var s = model.Summary;
        var panel = new StackPanel();

        var cards = new Grid();
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

        AddCard(cards, 0, "TỔNG SỐ XE", $"{s.TicketCount.ToString("N0", Vn)} lượt", $"{model.DistinctVehicles.ToString("N0", Vn)} xe khác nhau");
        AddCard(cards, 1, "TỔNG HÀNG", $"{s.TotalNetWeightKg.ToString("N0", Vn)} kg",
            s.TotalBillableWeightKg != s.TotalNetWeightKg ? $"Tính tiền {s.TotalBillableWeightKg.ToString("N0", Vn)} kg" : null);
        AddCard(cards, 2, "THÀNH TIỀN", $"{s.TotalAmountVnd.ToString("N0", Vn)} VNĐ", null, emphasize: true);
        panel.Children.Add(cards);

        if (s.MissingPriceCount > 0)
            panel.Children.Add(Note($"Lưu ý: {s.MissingPriceCount.ToString("N0", Vn)} phiếu chưa có đơn giá, chưa tính vào thành tiền."));
        if (s.AwaitingSecondWeighCount > 0)
            panel.Children.Add(Note($"Lưu ý: {s.AwaitingSecondWeighCount.ToString("N0", Vn)} phiếu đang chờ cân lần 2."));

        // Only the customer confirms the report: one signature block on the right.
        var signature = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = 360,
            Margin = new Thickness(0, 8, 40, 0)
        };
        var date = Text(
            $"Ngày {model.PrintedAt.Day} tháng {model.PrintedAt.Month} năm {model.PrintedAt.Year}",
            18, FontWeights.Normal, TextAlignment.Center, Ink);
        date.FontStyle = FontStyles.Italic;
        signature.Children.Add(date);
        signature.Children.Add(Text("Khách hàng xác nhận", 20, FontWeights.Bold, TextAlignment.Center, Ink));
        signature.Children.Add(new Border { Height = 60 });
        panel.Children.Add(signature);
        return panel;
    }

    private static void AddCard(Grid grid, int column, string label, string value, string? sub, bool emphasize = false)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(Text(label, 18, FontWeights.SemiBold, TextAlignment.Center, emphasize ? Brushes.White : Muted));
        stack.Children.Add(Text(value, emphasize ? 36 : 34, FontWeights.ExtraBold, TextAlignment.Center, emphasize ? Brushes.White : Ink));
        if (sub is not null)
            stack.Children.Add(Text(sub, 16, FontWeights.SemiBold, TextAlignment.Center, emphasize ? Brushes.White : Muted));

        var card = new Border
        {
            Child = stack,
            Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0),
            Padding = new Thickness(8, 8, 8, 8),
            CornerRadius = new CornerRadius(10),
            Background = emphasize ? Accent : Stripe,
            BorderBrush = emphasize ? Accent : Hairline,
            BorderThickness = new Thickness(1.5),
            MinHeight = 92
        };
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private static TextBlock Note(string text)
    {
        var note = Text(text, 16, FontWeights.SemiBold, TextAlignment.Left, Muted);
        note.Margin = new Thickness(0, 8, 0, 0);
        return note;
    }

    private static string Number(decimal? value) => value.HasValue ? value.Value.ToString("N0", Vn) : "—";

    private static Border BuildHeaderRow(IReadOnlyList<Column> columns, TableLayout layout, double width) =>
        BuildRow(layout, width, columns.Select(c => c.Header).ToArray(), isHeader: true, striped: false);

    private static Border BuildDataRow(IReadOnlyList<Column> columns, TableLayout layout, ReportRowDto row, double width, bool striped) =>
        BuildRow(layout, width, columns.Select(c => c.Cell(row)).ToArray(), isHeader: false, striped);

    private static Border BuildRow(TableLayout layout, double width, string[] cells, bool isHeader, bool striped)
    {
        var grid = new Grid { Width = width };
        foreach (var columnWidth in layout.Widths)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columnWidth, GridUnitType.Star) });

        for (var c = 0; c < cells.Length; c++)
        {
            var text = Text(
                cells[c],
                isHeader ? layout.HeaderFontSize : layout.FontSize,
                isHeader ? FontWeights.Bold : FontWeights.SemiBold,
                TextAlignment.Center,
                isHeader ? Brushes.White : Ink);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.Margin = new Thickness(4, 0, 4, 0);
            if (!isHeader && layout.Wrap)
                text.TextWrapping = TextWrapping.Wrap;
            else
                text.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(text, c);
            grid.Children.Add(text);
        }

        return new Border
        {
            Height = isHeader ? layout.HeaderHeight : layout.RowHeight,
            Child = grid,
            Background = isHeader ? Accent : striped ? Stripe : Brushes.White,
            BorderBrush = Hairline,
            BorderThickness = isHeader ? new Thickness(0) : new Thickness(0, 0, 0, 1)
        };
    }

    private static TextBlock Text(string text, double size, FontWeight weight, TextAlignment alignment, Brush foreground) =>
        new()
        {
            Text = text,
            FontFamily = Font,
            FontSize = size,
            FontWeight = weight,
            Foreground = foreground,
            TextAlignment = alignment,
            TextWrapping = TextWrapping.NoWrap
        };

    private static double MeasureHeight(UIElement element, double width)
    {
        element.Measure(new Size(width, double.PositiveInfinity));
        return element.DesiredSize.Height;
    }
}
