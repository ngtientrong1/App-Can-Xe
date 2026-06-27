using System.IO;
using System.Text;
using CanXe.Domain.Models;

namespace CanXe.Desktop.Services;

public static class ScaleModeLogger
{
    public static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CanXe",
            "Logs");

    public static string LogFilePath => Path.Combine(LogDirectory, "scale-mode.log");

    public static void Write(
        string eventName,
        string deviceModeFromAppSettings,
        ScaleInputMode? scaleInputModeFromDb,
        ScaleInputMode? selectedUiMode,
        ScaleInputMode activeRuntimeMode,
        ScaleInputMode compositeServiceMode,
        bool simulationRunning,
        bool comConnected)
    {
        Directory.CreateDirectory(LogDirectory);
        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Event: {eventName}");
        builder.AppendLine($"DeviceMode from appsettings: {deviceModeFromAppSettings}");
        builder.AppendLine($"ScaleInputMode from DB: {FormatMode(scaleInputModeFromDb)}");
        builder.AppendLine($"Selected radio mode: {FormatMode(selectedUiMode)}");
        builder.AppendLine($"Active runtime mode: {FormatMode(activeRuntimeMode)}");
        builder.AppendLine($"CompositeScaleService mode: {FormatMode(compositeServiceMode)}");
        builder.AppendLine($"Simulation running: {(simulationRunning ? "yes" : "no")}");
        builder.AppendLine($"COM connected: {(comConnected ? "yes" : "no")}");
        builder.AppendLine(new string('-', 60));
        File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
    }

    public static void WriteNormalization(
        ScaleInputMode? previousMode,
        ScaleInputMode normalizedMode,
        string deviceMode)
    {
        Directory.CreateDirectory(LogDirectory);
        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine("Legacy DB scale mode normalized:");
        builder.AppendLine($"{FormatMode(previousMode)} → {FormatMode(normalizedMode)}");
        builder.AppendLine($"Reason: DeviceMode={deviceMode}");
        builder.AppendLine(new string('-', 60));
        File.AppendAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
    }

    private static string FormatMode(ScaleInputMode? mode) =>
        mode?.ToString() ?? "(null)";
}
