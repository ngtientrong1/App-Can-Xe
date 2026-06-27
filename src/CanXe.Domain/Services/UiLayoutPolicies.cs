namespace CanXe.Domain.Services;

public static class CompactLayoutPolicy
{
    public const double CompactWidthThreshold = 1366;

    public static bool ShouldUseCompactMode(double screenWidth) => screenWidth <= CompactWidthThreshold;
}

public static class EditModeActionBarPolicy
{
    public static bool IsCreateModeVisible(bool isEditingExistingTicket) => !isEditingExistingTicket;
    public static bool IsEditModeVisible(bool isEditingExistingTicket) => isEditingExistingTicket;
}

public static class NavigationTabPolicy
{
    public static bool IsTabActive(string activeSectionKey, string tabKey) =>
        string.Equals(activeSectionKey, tabKey, StringComparison.Ordinal);

    public static string GetDefaultSectionKey() => "WeighTicket";
}

public static class WorkAreaVisualStatePolicy
{
    public static bool CameraDrawerLeavesNoGapWhenClosed(int cameraDrawerWidthPixels) =>
        cameraDrawerWidthPixels == 0;

    public static bool UtilityRailAlwaysVisible(int railWidthPixels) => railWidthPixels > 0;

    public static int GetSummaryCardCount() => 5;
}
