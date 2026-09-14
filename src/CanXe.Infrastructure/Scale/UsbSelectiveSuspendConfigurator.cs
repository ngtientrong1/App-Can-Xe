using System.Diagnostics;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Scale;

/// <summary>
/// Disables Windows' "USB selective suspend" power-saving policy on the active power plan.
/// Root-causes the PL2303HXD (and similar cheap USB-to-serial clones) going silent until a
/// physical unplug/replug: Windows periodically suspends an idle-looking USB device to save
/// power, and these chips' drivers frequently fail to resume cleanly from that suspend — a
/// different, more common failure mode than the "wedged UART" scenario <see cref="UsbHubPortCycler"/>
/// and <see cref="WindowsPnpSerialPortResetter"/> already recover from. Keeping selective suspend
/// off keeps the device continuously powered/active so it never enters that state in the first
/// place, which is the software equivalent of the user's own suggested fix (keep the port
/// continuously read/written so it "stays alive").
/// </summary>
/// <remarks>
/// Uses <c>powercfg</c> against the officially documented USB settings power-scheme GUIDs — the
/// same values Windows' own Power Options UI ("USB settings" > "USB selective suspend setting")
/// writes. Changing AC/DC values on the currently active scheme is a per-user preference and does
/// not require elevation, so this can run every app startup as cheap, idempotent insurance against
/// the setting being reset (e.g. by a Windows Update power-plan reset) after install. The installer
/// also applies it once at install time so the fix is in effect even before the app's first launch.
/// </remarks>
public static class UsbSelectiveSuspendConfigurator
{
    private const string UsbSettingsSubgroupGuid = "2a737441-1930-4402-8d77-b2bebba308a3";
    private const string SelectiveSuspendSettingGuid = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

    /// <summary>Best-effort, never throws. Safe to call fire-and-forget from app startup.</summary>
    public static void DisableOnActiveScheme()
    {
        try
        {
            var ac = RunPowercfg($"/setacvalueindex SCHEME_CURRENT {UsbSettingsSubgroupGuid} {SelectiveSuspendSettingGuid} 0");
            var dc = RunPowercfg($"/setdcvalueindex SCHEME_CURRENT {UsbSettingsSubgroupGuid} {SelectiveSuspendSettingGuid} 0");
            var applied = RunPowercfg("/setactive SCHEME_CURRENT");

            LifecycleLogger.Write(
                "UsbSelectiveSuspend",
                ac && dc && applied ? "disabled" : $"partial:ac={ac}:dc={dc}:apply={applied}");
        }
        catch (Exception ex)
        {
            LifecycleLogger.Write("UsbSelectiveSuspend", $"error:{ex.GetType().Name}:{ex.Message}");
        }
    }

    private static bool RunPowercfg(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            process.WaitForExit(5000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
