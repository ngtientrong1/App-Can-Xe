namespace CanXe.Domain.Services;

public static class WorkAreaLayoutCalculator
{
    public const int CollapsedCameraColumnPixels = 40;

    public static (int WeighStars, int InfoStars, int CameraStars) GetColumnStars(bool isCameraPanelVisible) =>
        isCameraPanelVisible ? (32, 50, 18) : (34, 66, 0);

    public static bool HasUnusedCameraColumnGap(bool isCameraPanelVisible, int cameraColumnMinWidth) =>
        !isCameraPanelVisible && cameraColumnMinWidth > 0;
}
