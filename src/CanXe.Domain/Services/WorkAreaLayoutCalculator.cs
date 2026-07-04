namespace CanXe.Domain.Services;

public static class WorkAreaLayoutCalculator
{
    public const int MinWeighColumnPercent = 42;

    public static (int WeighStars, int InfoStars) GetMainColumnStars(double windowWidth = 1920) =>
        windowWidth switch
        {
            >= 1900 => (44, 56),
            >= 1366 => (46, 54),
            _ => (50, 50)
        };

    public static double GetLiveWeightFontSize(double windowWidth) =>
        windowWidth switch
        {
            >= 1900 => 84,
            >= 1580 => 76,
            >= 1366 => 68,
            _ => 64
        };

    public static double GetLiveWeightUnitFontSize(double windowWidth) =>
        windowWidth switch
        {
            >= 1900 => 26,
            >= 1580 => 24,
            >= 1366 => 22,
            _ => 20
        };

    public static double GetWeighPanelValueFontSize(double windowWidth) =>
        windowWidth switch
        {
            >= 1900 => 28,
            >= 1366 => 24,
            _ => 22
        };

    public static double GetSummaryValueFontSize(double windowWidth) =>
        windowWidth switch
        {
            >= 1900 => 32,
            >= 1366 => 28,
            _ => 24
        };

    [Obsolete("Camera drawer removed in Phase 4.")]
    public static (int WeighStars, int InfoStars) GetMainColumnStars(bool isCameraDrawerOpen) =>
        GetMainColumnStars();

    [Obsolete("Utility rail removed in Phase 4.")]
    public const int UtilityRailWidthPixels = 0;

    [Obsolete("Camera drawer removed in Phase 4.")]
    public static int GetCameraDrawerWidth(bool isOpen, double windowWidth = 1920) => 0;

    [Obsolete("Utility rail removed in Phase 4.")]
    public static int GetUtilityRailWidth(bool isCompact) => 0;
}
