using CanXe.Infrastructure.Scale;

namespace CanXe.ComResetHelper;

/// <summary>
/// Tiny elevated helper triggered by the "CanXeTienTrong\ResetComPort" Scheduled Task (installed by
/// installer/CanXeTienTrong.iss to run as SYSTEM, with a security descriptor that lets a standard
/// user trigger it via "schtasks /Run"). The main app (CanXe.Desktop, running as a standard user)
/// writes the target COM port name to <see cref="ComResetIpc.RequestFilePath"/> and starts the task
/// via <see cref="ScheduledTaskSerialPortResetter"/>; this process does the actual privileged
/// pnputil disable/enable and writes the outcome back to <see cref="ComResetIpc.ResultFilePath"/>.
/// </summary>
internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            if (!File.Exists(ComResetIpc.RequestFilePath))
            {
                await WriteResultAsync("error: no request file");
                return 1;
            }

            var portName = (await File.ReadAllTextAsync(ComResetIpc.RequestFilePath)).Trim();
            if (string.IsNullOrWhiteSpace(portName))
            {
                await WriteResultAsync("error: empty port name");
                return 1;
            }

            var resetter = new WindowsPnpSerialPortResetter();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var ok = await resetter.TryResetAsync(portName, cts.Token);

            await WriteResultAsync(ok ? "ok" : $"failed: reset unsuccessful for {portName}");
            return ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            await WriteResultAsync($"error: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static async Task WriteResultAsync(string content)
    {
        try
        {
            Directory.CreateDirectory(ComResetIpc.RootDirectory);
            await File.WriteAllTextAsync(ComResetIpc.ResultFilePath, content);
        }
        catch
        {
            // Best effort — the caller times out on its own if this never appears.
        }
    }
}
