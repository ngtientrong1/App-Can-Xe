namespace CanXe.Domain.Services;

public static class WorkAreaLayoutCalculator
{
    public static (int WeighStars, int InfoStars, int CameraStars) GetColumnStars(bool isCameraPanelVisible) =>
        isCameraPanelVisible ? (32, 50, 18) : (32, 68, 0);

    public static bool HasUnusedCameraColumnGap(bool isCameraPanelVisible, int cameraColumnMinWidth) =>
        !isCameraPanelVisible && cameraColumnMinWidth > 0;
}
