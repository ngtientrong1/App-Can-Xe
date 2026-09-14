using System.Diagnostics;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Scale;

/// <summary>
/// Used by the (standard-user, non-elevated) main app to recover a wedged USB-to-serial adapter
/// without running the whole app elevated. Drops the target port name in a well-known file, kicks
/// off a pre-registered Scheduled Task (installed with "SYSTEM" run-level and a security descriptor
/// that lets any user trigger it — see installer/CanXeTienTrong.iss), and waits for
/// CanXe.ComResetHelper.exe (run by that task) to write back a result.
/// </summary>
public sealed class ScheduledTaskSerialPortResetter : ISerialPortResetter
{
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    public async Task<bool> TryResetAsync(string portName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        try
        {
            Directory.CreateDirectory(ComResetIpc.RootDirectory);
            if (File.Exists(ComResetIpc.ResultFilePath))
                File.Delete(ComResetIpc.ResultFilePath);
            await File.WriteAllTextAsync(ComResetIpc.RequestFilePath, portName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LifecycleLogger.Write("UsbSerialReset", $"ipcWriteError:{portName}:{ex.GetType().Name}");
            return false;
        }

        if (!await RunSchTasksAsync(cancellationToken).ConfigureAwait(false))
        {
            LifecycleLogger.Write("UsbSerialReset", $"schtasksRunFailed:{portName}");
            return false;
        }

        var deadline = DateTimeOffset.UtcNow + ResultTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(ComResetIpc.ResultFilePath))
                {
                    var content = (await File.ReadAllTextAsync(ComResetIpc.ResultFilePath, cancellationToken)
                        .ConfigureAwait(false)).Trim();
                    var ok = content.StartsWith("ok", StringComparison.OrdinalIgnoreCase);
                    LifecycleLogger.Write("UsbSerialReset", ok ? $"ok:{portName}" : $"failed:{portName}:{content}");
                    return ok;
                }
            }
            catch (IOException)
            {
                // Helper may still be writing the file — retry on the next poll.
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        LifecycleLogger.Write("UsbSerialReset", $"timeout:{portName}");
        return false;
    }

    private static async Task<bool> RunSchTasksAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("/Run");
            startInfo.ArgumentList.Add("/TN");
            startInfo.ArgumentList.Add(ComResetIpc.ScheduledTaskName);

            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            LifecycleLogger.Write("UsbSerialReset", $"schtasksError:{ex.GetType().Name}");
            return false;
        }
    }
}
