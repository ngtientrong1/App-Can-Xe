namespace CanXe.DeviceTester;

public static class DeviceTesterBindingPolicy
{
    public static IReadOnlyList<string> ReadOnlyDisplayProperties { get; } =
    [
        nameof(ViewModels.DeviceTesterViewModel.LogDirectory),
        nameof(ViewModels.DeviceTesterViewModel.StatusDisplay),
        nameof(ViewModels.DeviceTesterViewModel.StatsLine),
        nameof(ViewModels.DeviceTesterViewModel.LatestHex),
        nameof(ViewModels.DeviceTesterViewModel.LatestEscaped),
        nameof(ViewModels.DeviceTesterViewModel.EventChunks),
        nameof(ViewModels.DeviceTesterViewModel.AccumulatedHex),
        nameof(ViewModels.DeviceTesterViewModel.AccumulatedEscaped),
        nameof(ViewModels.DeviceTesterViewModel.LastSavedLogPath),
        nameof(ViewModels.DeviceTesterViewModel.WarningBanner),
        nameof(ViewModels.DeviceTesterViewModel.IsConfigurationEnabled),
        nameof(ViewModels.DeviceTesterViewModel.IsPortOpen),
        nameof(ViewModels.DeviceTesterViewModel.IsRecording),
        nameof(ViewModels.DeviceTesterViewModel.IsDisplayPaused)
    ];

    public static bool RequiresOneWayBinding(string propertyName) =>
        ReadOnlyDisplayProperties.Contains(propertyName, StringComparer.Ordinal);
}
