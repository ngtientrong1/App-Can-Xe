namespace CanXe.Domain.Services;

public static class OperatorLayoutMetrics
{
    public const double DataGridHeaderHeight = 46;
    public const double DataGridRowHeight = 44;
    public const double SummaryFooterMaxHeight = 72;
    public const double StatusBarHeight = 38;
    public const double ActionBarHeight = 60;
    public const double FilterBarCompactHeight = 52;
    public const double FilterBarExpandedHeight = 128;
    public const double AppHeaderHeight = 72;
    public const double NavigationBarHeight = 52;
    public const double PageVerticalMargin = 16;

    public static double GetWorkspaceMaxHeight(double windowHeight)
    {
        if (windowHeight < 850)
            return 420;

        if (windowHeight < 1200)
            return 460;

        var usable = GetUsableContentHeight(windowHeight);
        var target = usable * 0.40;
        return Math.Clamp(target, 420, 480);
    }

    public static double GetWorkspaceMinHeight(double windowHeight) =>
        windowHeight < 850 ? 260 : 360;

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
        windowHeight - AppHeaderHeight - NavigationBarHeight - PageVerticalMargin;
}
