using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Desktop.Views.Printing;
using CanXe.Domain.Services;

namespace CanXe.Desktop.Services;

public sealed class PrintLayoutGeometryReport
{
    public bool Passed { get; init; }
    public IReadOnlyList<string> Lines { get; init; } = [];
    public int IntersectionCount { get; init; }
    public int TouchCount { get; init; }
    public int MissingEdgeCount { get; init; }
    public int DoubleBorderCount { get; init; }
    public int ConnectedCardBorderCount { get; init; }
    public double HeroToBodyGapMm { get; init; }
    public double CustomerVehicleGapMm { get; init; }
    public double IdentityDetailsGapMm { get; init; }
    public double VehicleTimestampGapMm { get; init; }
    public double BodySignatureGapMm { get; init; }
    public double RightEdgeClearancePx { get; init; }
    public double LeftEdgeClearancePx { get; init; }
    public IReadOnlyDictionary<string, int> CardEdgeCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
}

public static class PrintLayoutGeometryValidator
{
    private const double Tolerance = WeighTicketPrintLayout.GeometryToleranceDip;
    private static readonly string[] CardNames =
    [
        "HeroCardGross",
        "HeroCardTare",
        "HeroCardNet",
        "CustomerIdentityCard",
        "PlateIdentityCard",
        "DetailsTableCard",
        "TimestampCard"
    ];

    public static PrintLayoutGeometryReport ValidateCopy(WeighTicketCopyView copy)
    {
        WpfWeighTicketDocumentFactory.Materialize(copy);
        return ValidateMaterializedCopy(copy);
    }

    public static PrintLayoutGeometryReport ValidateMaterializedCopy(WeighTicketCopyView copy) =>
        ValidateMaterializedCopy(copy, "Copy");

    public static PrintLayoutGeometryReport ValidateMaterializedCopy(WeighTicketCopyView copy, string label)
    {
        var lines = new List<string> { $"--- {label} ---" };
        var failures = 0;
        var intersections = 0;
        var touches = 0;

        var outer = FindNamed<Border>(copy, "OuterFrame");
        if (outer is not null)
        {
            failures++;
            lines.Add("FAIL: OuterFrame must be removed");
        }
        else
        {
            lines.Add("Outer frame absent: True");
        }

        var hero1 = FindNamed<Border>(copy, "HeroCardGross");
        var hero2 = FindNamed<Border>(copy, "HeroCardTare");
        var hero3 = FindNamed<Border>(copy, "HeroCardNet");
        var customer = FindNamed<Border>(copy, "CustomerIdentityCard");
        var vehicle = FindNamed<Border>(copy, "PlateIdentityCard");
        var details = FindNamed<Border>(copy, "DetailsTableCard");
        var timestamp = FindNamed<Border>(copy, "TimestampCard");
        var signDateRow = FindNamed<FrameworkElement>(copy, "SignDateRowPanel");
        var signatureSeparator = FindNamed<Border>(copy, "SignatureTopSeparator");

        var cards = new Dictionary<string, Border>(StringComparer.Ordinal)
        {
            ["HeroGross"] = hero1!,
            ["HeroTare"] = hero2!,
            ["HeroNet"] = hero3!,
            ["Customer"] = customer!,
            ["Vehicle"] = vehicle!,
            ["Details"] = details!,
            ["Timestamp"] = timestamp!
        };

        foreach (var (shortName, card) in cards)
        {
            if (card is null)
            {
                failures++;
                lines.Add($"FAIL: Missing card {shortName}");
                continue;
            }

            LogBounds(lines, card.Name, GetBounds(copy, card));
            failures += AssertCardHost(copy, card);
        }

        var heroToBodyGapMm = 0.0;
        var customerVehicleGapMm = 0.0;
        var identityDetailsGapMm = 0.0;
        var vehicleTimestampGapMm = 0.0;
        var bodySignatureGapMm = 0.0;

        if (hero1 is not null && hero2 is not null && hero3 is not null)
        {
            failures += AssertSameY(lines, copy, "Hero", hero1, hero2, hero3);
            failures += AssertMinGapMm(lines, copy, "Hero1-Hero2", hero1, hero2, WeighTicketPrintLayout.WeightCardGapMm);
            failures += AssertMinGapMm(lines, copy, "Hero2-Hero3", hero2, hero3, WeighTicketPrintLayout.WeightCardGapMm);
        }

        if (customer is not null && vehicle is not null)
        {
            failures += AssertSameY(lines, copy, "Identity", customer, vehicle);
            customerVehicleGapMm = MeasureGapMm(copy, customer, vehicle, horizontal: true);
            failures += AssertGapAtLeastMm(lines, "Customer-Vehicle", customerVehicleGapMm, WeighTicketPrintLayout.IdentityCardGapMm);
        }

        if (customer is not null && details is not null)
        {
            failures += AssertAlignedLeft(lines, customer, details, copy);
            identityDetailsGapMm = MeasureGapMm(copy, customer, details, horizontal: false);
            failures += AssertGapAtLeastMm(lines, "Identity-Details", identityDetailsGapMm, WeighTicketPrintLayout.IdentityDetailsGapMm);
        }

        if (vehicle is not null && details is not null)
            failures += AssertAlignedRight(lines, vehicle, details, copy);

        if (customer is not null && timestamp is not null)
            failures += AssertSameTop(lines, customer, timestamp, copy);

        if (vehicle is not null && timestamp is not null)
        {
            vehicleTimestampGapMm = MeasureGapMm(copy, vehicle, timestamp, horizontal: true);
            failures += AssertGapAtLeastMm(lines, "Vehicle-Timestamp", vehicleTimestampGapMm, WeighTicketPrintLayout.BodyLeftTimestampGapMm);
        }

        if (hero1 is not null && customer is not null && timestamp is not null)
        {
            var heroBottom = Math.Max(GetBounds(copy, hero1).Bottom, Math.Max(GetBounds(copy, hero2!).Bottom, GetBounds(copy, hero3!).Bottom));
            var bodyTop = Math.Min(GetBounds(copy, customer).Top, GetBounds(copy, timestamp).Top);
            heroToBodyGapMm = WeighTicketPrintLayout.DipToMm(bodyTop - heroBottom);
            lines.Add($"Gap Hero-to-body: {heroToBodyGapMm:F2} mm");
            if (heroToBodyGapMm < WeighTicketPrintLayout.HeroBodyGapMm - WeighTicketPrintLayout.DipToMm(Tolerance))
            {
                failures++;
                lines.Add($"FAIL: Hero-to-body gap {heroToBodyGapMm:F2}mm < {WeighTicketPrintLayout.HeroBodyGapMm}mm");
            }
        }

        if (details is not null && signatureSeparator is not null)
        {
            var bodyBottom = Math.Max(GetBounds(copy, details).Bottom, signDateRow is null ? 0 : GetBounds(copy, signDateRow).Bottom);
            bodySignatureGapMm = WeighTicketPrintLayout.DipToMm(GetBounds(copy, signatureSeparator).Top - bodyBottom);
            lines.Add($"Gap Body-Signature: {bodySignatureGapMm:F2} mm");
            if (bodySignatureGapMm < WeighTicketPrintLayout.BodySignatureGapMm - WeighTicketPrintLayout.DipToMm(Tolerance))
            {
                failures++;
                lines.Add($"FAIL: Body-Signature gap {bodySignatureGapMm:F2}mm < {WeighTicketPrintLayout.BodySignatureGapMm}mm");
            }
        }

        var cardElements = cards.Values.Where(c => c is not null).Cast<FrameworkElement>().ToList();
        for (var i = 0; i < cardElements.Count; i++)
        {
            for (var j = i + 1; j < cardElements.Count; j++)
            {
                var a = GetBounds(copy, cardElements[i]);
                var b = GetBounds(copy, cardElements[j]);
                if (RectsIntersect(a, b))
                {
                    intersections++;
                    failures++;
                    lines.Add($"FAIL: {cardElements[i].Name} intersects {cardElements[j].Name}");
                }

                if (RectsTouch(a, b))
                {
                    touches++;
                    failures++;
                    lines.Add($"FAIL: {cardElements[i].Name} touches {cardElements[j].Name}");
                }
            }
        }

        var edgeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var missingEdges = 0;
        foreach (var (shortName, card) in cards)
        {
            if (card is null)
                continue;

            var edges = CountLogicalEdges(card);
            edgeCounts[shortName] = edges;
            lines.Add($"{shortName}: edges={edges}");
            if (edges != 4)
            {
                missingEdges += 4 - edges;
                failures++;
                lines.Add($"FAIL: {shortName} edge count {edges} != 4");
            }
        }

        lines.Add($"HeroToBodyGapMm: {heroToBodyGapMm:F2}");
        lines.Add($"CustomerVehicleGapMm: {customerVehicleGapMm:F2}");
        lines.Add($"IdentityDetailsGapMm: {identityDetailsGapMm:F2}");
        lines.Add($"VehicleTimestampGapMm: {vehicleTimestampGapMm:F2}");
        lines.Add($"BodySignatureGapMm: {bodySignatureGapMm:F2}");
        lines.Add($"IntersectionCount: {intersections}");
        lines.Add($"TouchCount: {touches}");
        lines.Add($"MissingEdgeCount: {missingEdges}");
        lines.Add($"DoubleBorderCount: 0");
        lines.Add($"ConnectedCardBorderCount: {touches}");
        lines.Add($"Card border thickness: {WeighTicketPrintLayout.CardBorderThicknessDip} DIP");
        lines.Add($"Corner radius: {WeighTicketPrintLayout.CardCornerRadiusMm} mm");
        lines.Add($"CardHostInset: {WeighTicketPrintLayout.CardHostInsetMm} mm");
        lines.Add(failures == 0 ? "PASS" : "FAIL");

        return new PrintLayoutGeometryReport
        {
            Passed = failures == 0,
            Lines = lines,
            IntersectionCount = intersections,
            TouchCount = touches,
            MissingEdgeCount = missingEdges,
            DoubleBorderCount = 0,
            ConnectedCardBorderCount = touches,
            HeroToBodyGapMm = heroToBodyGapMm,
            CustomerVehicleGapMm = customerVehicleGapMm,
            IdentityDetailsGapMm = identityDetailsGapMm,
            VehicleTimestampGapMm = vehicleTimestampGapMm,
            BodySignatureGapMm = bodySignatureGapMm,
            CardEdgeCounts = edgeCounts
        };
    }

    public static PrintLayoutGeometryReport ValidateRaster(BitmapSource bitmap, RasterPlacement placement)
    {
        var lines = new List<string>();
        var analysis = RasterEdgeClearanceAnalyzer.Analyze(bitmap, placement);
        lines.Add($"Raster target: L={placement.TargetLeftDip:F1} T={placement.TargetTopDip:F1} W={placement.TargetWidthDip:F1} H={placement.TargetHeightDip:F1}");
        lines.Add($"Raster placement: L={placement.LeftDip:F1} T={placement.TopDip:F1} W={placement.WidthDip:F1} H={placement.HeightDip:F1} scale={placement.ScaleFactor:F4}");
        lines.Add($"Left-edge clearance: {analysis.LeftClearancePx:F0} px");
        lines.Add($"Right-edge clearance: {analysis.RightClearancePx:F0} px");
        lines.Add($"Top-edge clearance: {analysis.TopClearancePx:F0} px");
        lines.Add($"Bottom-edge clearance: {analysis.BottomClearancePx:F0} px");
        lines.Add($"SinglePassMapping: {placement.SinglePassMapping}");

        var failures = 0;
        if (analysis.LeftClearancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Left clearance {analysis.LeftClearancePx:F0}px < {WeighTicketPrintLayout.MinRasterEdgeClearancePx}px");
        }

        if (analysis.RightClearancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Right clearance {analysis.RightClearancePx:F0}px < {WeighTicketPrintLayout.MinRasterEdgeClearancePx}px");
        }

        if (analysis.TopClearancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Top clearance {analysis.TopClearancePx:F0}px < {WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx}px");
        }

        if (analysis.BottomClearancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Bottom clearance {analysis.BottomClearancePx:F0}px < {WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx}px");
        }

        lines.Add(failures == 0 ? "PASS" : "FAIL");
        return new PrintLayoutGeometryReport
        {
            Passed = failures == 0,
            Lines = lines,
            IntersectionCount = 0,
            MissingEdgeCount = analysis.MissingEdgeCount,
            RightEdgeClearancePx = analysis.RightClearancePx,
            LeftEdgeClearancePx = analysis.LeftClearancePx
        };
    }

    public static PrintLayoutGeometryReport ValidateRasterA5Ticket(BitmapSource bitmap, RasterPlacement placement)
    {
        var lines = new List<string>();
        var analysis = RasterEdgeClearanceAnalyzer.AnalyzeA5Ticket(bitmap, placement);
        lines.Add($"Raster target: L={placement.TargetLeftDip:F1} T={placement.TargetTopDip:F1} W={placement.TargetWidthDip:F1} H={placement.TargetHeightDip:F1}");
        lines.Add($"Raster placement: L={placement.LeftDip:F1} T={placement.TopDip:F1} W={placement.WidthDip:F1} H={placement.HeightDip:F1} scale={placement.ScaleFactor:F4}");
        lines.Add($"Left-edge clearance: {analysis.LeftClearancePx:F0} px");
        lines.Add($"Right-edge clearance: {analysis.RightClearancePx:F0} px");
        lines.Add($"Top-edge clearance: {analysis.TopClearancePx:F0} px");
        lines.Add($"Bottom-edge clearance: {analysis.BottomClearancePx:F0} px");
        lines.Add($"SinglePassMapping: {placement.SinglePassMapping}");

        var failures = 0;
        if (analysis.LeftClearancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Left clearance {analysis.LeftClearancePx:F0}px < {WeighTicketPrintLayout.MinRasterEdgeClearancePx}px");
        }

        if (analysis.RightClearancePx < WeighTicketPrintLayout.MinRasterEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Right clearance {analysis.RightClearancePx:F0}px < {WeighTicketPrintLayout.MinRasterEdgeClearancePx}px");
        }

        if (analysis.TopClearancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Top clearance {analysis.TopClearancePx:F0}px < {WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx}px");
        }

        if (analysis.BottomClearancePx < WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx)
        {
            failures++;
            lines.Add($"FAIL: Bottom clearance {analysis.BottomClearancePx:F0}px < {WeighTicketPrintLayout.MinRasterVerticalEdgeClearancePx}px");
        }

        lines.Add(failures == 0 ? "PASS" : "FAIL");
        return new PrintLayoutGeometryReport
        {
            Passed = failures == 0,
            Lines = lines,
            IntersectionCount = 0,
            MissingEdgeCount = analysis.MissingEdgeCount,
            RightEdgeClearancePx = analysis.RightClearancePx,
            LeftEdgeClearancePx = analysis.LeftClearancePx
        };
    }

    public static PrintLayoutGeometryReport ValidateRasterCardEdges(
        BitmapSource bitmap,
        WeighTicketCopyView copy,
        string label)
    {
        var lines = new List<string> { $"--- Raster {label} ---" };
        var failures = 0;
        var cardBounds = new Dictionary<string, Rect>(StringComparer.Ordinal);
        foreach (var name in CardNames)
        {
            var card = FindNamed<Border>(copy, name);
            if (card is null)
                continue;

            cardBounds[name] = GetBounds(copy, card);
        }

        var namedBounds = cardBounds.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.Ordinal);
        var analysis = CardBorderRasterAnalyzer.AnalyzeCopyRegion(bitmap, Rect.Empty, namedBounds);

        var missing = 0;
        var displayNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HeroCardGross"] = "HeroGross",
            ["HeroCardTare"] = "HeroTare",
            ["HeroCardNet"] = "HeroNet",
            ["CustomerIdentityCard"] = "Customer",
            ["PlateIdentityCard"] = "Vehicle",
            ["DetailsTableCard"] = "Details",
            ["TimestampCard"] = "Timestamp"
        };

        foreach (var card in analysis.Cards)
        {
            var shortName = displayNames.GetValueOrDefault(card.CardName, card.CardName);
            lines.Add($"{shortName}: edges={card.EdgeCount}");
            if (card.EdgeCount != 4)
            {
                failures++;
                missing += 4 - card.EdgeCount;
            }
        }

        lines.Add($"MissingEdgeCount: {missing}");
        lines.Add($"DoubleBorderCount: {analysis.DoubleBorderCount}");
        lines.Add($"ConnectedCardBorderCount: {analysis.ConnectedCardBorderCount}");
        if (missing > 0)
            failures++;
        if (analysis.ConnectedCardBorderCount > 0)
            failures++;

        lines.Add(failures == 0 ? "PASS" : "FAIL");
        return new PrintLayoutGeometryReport
        {
            Passed = failures == 0,
            Lines = lines,
            MissingEdgeCount = missing,
            DoubleBorderCount = analysis.DoubleBorderCount,
            ConnectedCardBorderCount = analysis.ConnectedCardBorderCount
        };
    }

    public static string FormatReport(PrintLayoutGeometryReport report) =>
        string.Join(Environment.NewLine, report.Lines);

    private static void LogBounds(List<string> lines, string name, Rect bounds) =>
        lines.Add($"{name}: L={bounds.Left:F1} T={bounds.Top:F1} W={bounds.Width:F1} H={bounds.Height:F1} R={bounds.Right:F1} B={bounds.Bottom:F1}");

    private static Rect GetBounds(Visual root, FrameworkElement element)
    {
        var transform = element.TransformToAncestor(root);
        var topLeft = transform.Transform(new Point(0, 0));
        return new Rect(topLeft.X, topLeft.Y, element.ActualWidth, element.ActualHeight);
    }

    private static int CountLogicalEdges(Border card)
    {
        if (card.ActualWidth <= Tolerance || card.ActualHeight <= Tolerance)
            return 0;

        var t = card.BorderThickness;
        var min = WeighTicketPrintLayout.CardBorderThicknessDip - 0.1;
        if (t.Left < min || t.Top < min || t.Right < min || t.Bottom < min)
            return 0;

        if (card.ClipToBounds)
            return 0;

        return 4;
    }

    private static int AssertCardHost(Visual root, Border card)
    {
        if (VisualTreeHelper.GetParent(card) is not Border host)
            return 1;

        if (host.BorderThickness.Left > 0 || host.BorderThickness.Top > 0)
            return 1;

        var inset = WeighTicketPrintLayout.CardHostInsetDip;
        if (Math.Abs(host.Padding.Left - inset) > Tolerance ||
            Math.Abs(host.Padding.Top - inset) > Tolerance ||
            Math.Abs(host.Padding.Right - inset) > Tolerance ||
            Math.Abs(host.Padding.Bottom - inset) > Tolerance)
            return 1;

        if (host.ClipToBounds)
            return 1;

        var hostBounds = GetBounds(root, host);
        var cardBounds = GetBounds(root, card);
        if (cardBounds.Left < hostBounds.Left - Tolerance ||
            cardBounds.Top < hostBounds.Top - Tolerance ||
            cardBounds.Right > hostBounds.Right + Tolerance ||
            cardBounds.Bottom > hostBounds.Bottom + Tolerance)
            return 1;

        return 0;
    }

    private static int AssertSameY(List<string> lines, Visual root, string label, params FrameworkElement[] elements)
    {
        var tops = elements.Select(e => GetBounds(root, e).Top).ToList();
        var bottoms = elements.Select(e => GetBounds(root, e).Bottom).ToList();

        var failures = 0;
        if (tops.Max() - tops.Min() > Tolerance)
        {
            failures++;
            lines.Add($"FAIL: {label} tops misaligned (delta {tops.Max() - tops.Min():F2} DIP)");
        }

        if (bottoms.Max() - bottoms.Min() > Tolerance)
        {
            failures++;
            lines.Add($"FAIL: {label} bottoms misaligned (delta {bottoms.Max() - bottoms.Min():F2} DIP)");
        }

        return failures;
    }

    private static int AssertSameTop(List<string> lines, FrameworkElement a, FrameworkElement b, Visual root)
    {
        var ta = GetBounds(root, a).Top;
        var tb = GetBounds(root, b).Top;
        if (Math.Abs(ta - tb) <= Tolerance)
            return 0;

        lines.Add($"FAIL: Top misalignment {a.Name}/{b.Name} delta {Math.Abs(ta - tb):F2} DIP");
        return 1;
    }

    private static int AssertSameBottom(List<string> lines, FrameworkElement a, FrameworkElement b, Visual root)
    {
        var ba = GetBounds(root, a).Bottom;
        var bb = GetBounds(root, b).Bottom;
        if (Math.Abs(ba - bb) <= Tolerance)
            return 0;

        lines.Add($"FAIL: Bottom misalignment {a.Name}/{b.Name} delta {Math.Abs(ba - bb):F2} DIP");
        return 1;
    }

    private static int AssertAlignedLeft(List<string> lines, FrameworkElement a, FrameworkElement b, Visual root)
    {
        var la = GetBounds(root, a).Left;
        var lb = GetBounds(root, b).Left;
        if (Math.Abs(la - lb) <= Tolerance)
            return 0;

        lines.Add($"FAIL: Left misalignment {a.Name}/{b.Name} delta {Math.Abs(la - lb):F2} DIP");
        return 1;
    }

    private static int AssertAlignedRight(List<string> lines, FrameworkElement a, FrameworkElement b, Visual root)
    {
        var ra = GetBounds(root, a).Right;
        var rb = GetBounds(root, b).Right;
        if (Math.Abs(ra - rb) <= Tolerance)
            return 0;

        lines.Add($"FAIL: Right misalignment {a.Name}/{b.Name} delta {Math.Abs(ra - rb):F2} DIP");
        return 1;
    }

    private static double MeasureGapMm(Visual root, FrameworkElement first, FrameworkElement second, bool horizontal)
    {
        var a = GetBounds(root, first);
        var b = GetBounds(root, second);
        var gapDip = horizontal ? b.Left - a.Right : b.Top - a.Bottom;
        return WeighTicketPrintLayout.DipToMm(gapDip);
    }

    private static int AssertMinGapMm(
        List<string> lines,
        Visual root,
        string label,
        FrameworkElement left,
        FrameworkElement right,
        double minGapMm)
    {
        var gapMm = MeasureGapMm(root, left, right, horizontal: true);
        lines.Add($"Gap {label}: {gapMm:F2} mm");
        return AssertGapAtLeastMm(lines, label, gapMm, minGapMm);
    }

    private static int AssertGapAtLeastMm(List<string> lines, string label, double gapMm, double minGapMm)
    {
        if (gapMm >= minGapMm - WeighTicketPrintLayout.DipToMm(Tolerance))
            return 0;

        lines.Add($"FAIL: {label} gap {gapMm:F2}mm < {minGapMm}mm");
        return 1;
    }

    private static bool RectsIntersect(Rect a, Rect b)
    {
        var overlapWidth = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        var overlapHeight = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
        return overlapWidth > Tolerance && overlapHeight > Tolerance;
    }

    private static bool RectsTouch(Rect a, Rect b)
    {
        const double eps = 0.01;
        var horizontalOverlap = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left) > eps;
        var verticalOverlap = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top) > eps;
        var touchVertical = horizontalOverlap &&
                            (Math.Abs(a.Bottom - b.Top) <= eps || Math.Abs(b.Bottom - a.Top) <= eps);
        var touchHorizontal = verticalOverlap &&
                              (Math.Abs(a.Right - b.Left) <= eps || Math.Abs(b.Right - a.Left) <= eps);
        return touchVertical || touchHorizontal;
    }

    private static T? FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        if (root is T fe && fe.Name == name)
            return fe;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                var found = FindNamed<T>(child, name);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }
}
