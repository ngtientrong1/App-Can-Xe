namespace CanXe.Domain.Services;

public static class WorkAreaLayoutCalculator
{
    public const int UtilityRailWidthPixels = 56;
    public const int CameraDrawerWidthPixels = 300;
    public const int CompactUtilityRailWidthPixels = 52;

    public static (int WeighStars, int InfoStars) GetMainColumnStars(bool isCameraDrawerOpen) =>
        isCameraDrawerOpen ? (31, 51) : (31, 69);

    public static int GetUtilityRailWidth(bool isCompact) =>
        isCompact ? CompactUtilityRailWidthPixels : UtilityRailWidthPixels;

    public static int GetCameraDrawerWidth(bool isOpen) =>
        isOpen ? CameraDrawerWidthPixels : 0;

    [Obsolete("Use GetMainColumnStars + fixed rail/drawer columns")]
    public static (int WeighStars, int InfoStars, int CameraStars) GetColumnStars(bool isCameraPanelVisible) =>
        isCameraPanelVisible ? (32, 50, 18) : (34, 66, 0);

    [Obsolete("Use GetUtilityRailWidth")]
    public const int CollapsedCameraColumnPixels = 40;

    public static bool HasUnusedCameraColumnGap(bool isCameraPanelVisible, int cameraColumnMinWidth) =>
        !isCameraPanelVisible && cameraColumnMinWidth > 0;
}
