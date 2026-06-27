namespace CanXe.Domain.Services;

public static class WorkAreaLayoutCalculator
{
    public const int UtilityRailWidthPixels = 56;
    public const int CompactUtilityRailWidthPixels = 52;

    public const int CameraDrawerWidthWidePixels = 340;
    public const int CameraDrawerWidthMediumPixels = 280;
    public const int CameraDrawerWidthCompactPixels = 260;

    public const int CameraDrawerMaxWidthPixels = 360;
    public const int CameraDrawerMinWidthPixels = 260;

    public static (int WeighStars, int InfoStars) GetMainColumnStars(bool isCameraDrawerOpen) =>
        isCameraDrawerOpen ? (35, 45) : (38, 62);

    public static int GetUtilityRailWidth(bool isCompact) =>
        isCompact ? CompactUtilityRailWidthPixels : UtilityRailWidthPixels;

    public static int GetCameraDrawerWidth(bool isOpen, double windowWidth = 1920)
    {
        if (!isOpen)
            return 0;

        if (windowWidth >= 1500)
            return CameraDrawerWidthWidePixels;
        if (windowWidth >= 1200)
            return CameraDrawerWidthMediumPixels;
        return CameraDrawerWidthCompactPixels;
    }

    public static double GetLiveWeightFontSize(double windowWidth) =>
        windowWidth switch
        {
            >= 1900 => 96,
            >= 1580 => 84,
            >= 1366 => 72,
            _ => 64
        };

    public static double GetLiveWeightUnitFontSize(double windowWidth) =>
        windowWidth switch
        {
            >= 1900 => 32,
            >= 1580 => 28,
            >= 1366 => 24,
            _ => 20
        };

    public static bool ShouldUseCameraOverlay(double windowWidth) => windowWidth < 1200;

    [Obsolete("Use GetMainColumnStars + fixed rail/drawer columns")]
    public static (int WeighStars, int InfoStars, int CameraStars) GetColumnStars(bool isCameraPanelVisible) =>
        isCameraPanelVisible ? (32, 50, 18) : (34, 66, 0);

    [Obsolete("Use GetUtilityRailWidth")]
    public const int CollapsedCameraColumnPixels = 40;

    public static bool HasUnusedCameraColumnGap(bool isCameraPanelVisible, int cameraColumnMinWidth) =>
        !isCameraPanelVisible && cameraColumnMinWidth > 0;
}
