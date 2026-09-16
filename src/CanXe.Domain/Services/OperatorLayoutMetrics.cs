namespace CanXe.Domain.Services;

public static class OperatorLayoutMetrics
{
    public const double DataGridHeaderHeight = 50;
    public const double DataGridRowHeight = 52;
    public const double SummaryFooterMaxHeight = 72;
    public const double StatusBarHeight = 38;
    public const double ActionBarHeight = 60;
    public const double FilterBarCompactHeight = 52;
    public const double FilterBarExpandedHeight = 128;
    public const double AppHeaderHeight = 72;
    public const double NavigationBarHeight = 52;
    public const double PageVerticalMargin = 16;
    public const double KpiStripHeight = 84;

    public static double GetWorkspaceMaxHeight(double windowHeight)
    {
        if (windowHeight < 850)
            return 500;

        if (windowHeight < 1200)
            return 580;

        var usable = GetUsableContentHeight(windowHeight);
        var target = usable * 0.40;
        return Math.Clamp(target, 500, 620);
    }

    public static double GetWorkspaceMinHeight(double windowHeight) =>
        windowHeight < 850 ? 420 : 480;

    public static double GetDataGridMinHeight(double windowHeight) =>
        windowHeight < 850 ? 220 : 280;

    public static double GetLiveWeightFontSize(double windowWidth, double windowHeight)
    {
        var fromWidth = WorkAreaLayoutCalculator.GetLiveWeightFontSize(windowWidth);
        if (windowHeight >= 1200)
            return Math.Min(fromWidth, 88);

        if (windowHeight < 850)
            return Math.Min(fromWidth, 72);

        return fromWidth;
    }

    public static double GetLiveWeightViewboxMaxHeight(double windowHeight) =>
        windowHeight >= 1200 ? 100 : windowHeight >= 850 ? 120 : 96;

    public static double GetFormFieldHeight(double windowHeight) =>
        windowHeight < 850 ? 50 : 54;

    public static double GetNotesFieldHeight(double windowHeight) =>
        windowHeight < 850 ? 58 : 64;

    public static double GetFormRowSpacing(double windowHeight) =>
        windowHeight < 850 ? 8 : 10;

    public static double GetWorkspaceCardPadding(double windowHeight) =>
        windowHeight < 850 ? 14 : 16;

    public static double EstimateDataGridHeight(double windowHeight, bool advancedFilterVisible)
    {
        var usable = GetUsableContentHeight(windowHeight);
        var fixedHeight =
            GetWorkspaceMaxHeight(windowHeight)
            + StatusBarHeight
            + ActionBarHeight
            + (advancedFilterVisible ? FilterBarExpandedHeight : FilterBarCompactHeight)
            + SummaryFooterMaxHeight
            + 24;

        return Math.Max(GetDataGridMinHeight(windowHeight), usable - fixedHeight);
    }

    public static int EstimateVisibleDataGridRows(double windowHeight, bool advancedFilterVisible) =>
        EstimateRowsFromGridHeight(EstimateDataGridHeight(windowHeight, advancedFilterVisible));

    public static int EstimateRowsFromGridHeight(double gridHeight) =>
        (int)Math.Floor((gridHeight - DataGridHeaderHeight) / DataGridRowHeight);

    public static double GetUsableContentHeight(double windowHeight) =>
        windowHeight - AppHeaderHeight - NavigationBarHeight - PageVerticalMargin - KpiStripHeight;

    private const double WeighCanvasDesignWidth = 2209;
    private const double WeighCanvasDesignHeight = 1090;

    /// <summary>
    /// The Phiếu Cân section is a fixed-aspect design canvas scaled uniformly to fit the window
    /// (see MainWindow.xaml), calibrated so a typical maximized 1920x1080 window fills it with no
    /// letterboxing. On an ultrawide (21:9+) window the available area is proportionally wider than
    /// the canvas, so Stretch="Uniform" leaves empty bars on both sides. Widening the canvas to match
    /// the window's own aspect ratio (keeping the design height fixed) removes that letterboxing while
    /// leaving every proportion inside the canvas — fonts, paddings, column splits — unchanged, and has
    /// no effect on windows at or narrower than the design aspect ratio.
    /// </summary>
    public static double GetWeighCanvasWidth(double windowWidth, double windowHeight)
    {
        var availableHeight = windowHeight - AppHeaderHeight - NavigationBarHeight - PageVerticalMargin;
        if (availableHeight <= 0 || windowWidth <= 0)
            return WeighCanvasDesignWidth;

        var designAspect = WeighCanvasDesignWidth / WeighCanvasDesignHeight;
        var availableAspect = windowWidth / availableHeight;

        return availableAspect > designAspect
            ? WeighCanvasDesignHeight * availableAspect
            : WeighCanvasDesignWidth;
    }
}
