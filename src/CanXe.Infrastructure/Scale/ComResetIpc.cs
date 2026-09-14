namespace CanXe.Infrastructure.Scale;

/// <summary>
/// Well-known file paths and Scheduled Task name shared between the main app (standard user,
/// writes the request and triggers the task) and CanXe.ComResetHelper (runs elevated as SYSTEM via
/// the task, performs the actual pnputil disable/enable, writes the result).
/// </summary>
public static class ComResetIpc
{
    public const string ScheduledTaskName = @"CanXeTienTrong\ResetComPort";

    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CanXe");

    public static string RequestFilePath => Path.Combine(RootDirectory, "com-reset-request.txt");
    public static string ResultFilePath => Path.Combine(RootDirectory, "com-reset-result.txt");
}
