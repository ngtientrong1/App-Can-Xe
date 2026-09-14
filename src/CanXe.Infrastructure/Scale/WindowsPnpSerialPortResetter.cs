using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using CanXe.Infrastructure.Logging;

namespace CanXe.Infrastructure.Scale;

/// <summary>
/// Resets a wedged USB-serial device, escalating through three tiers of increasing strength.
/// Tries <see cref="UsbHubPortCycler"/> first — <c>IOCTL_USB_HUB_CYCLE_PORT</c> on the exact hub
/// port — since some USB-serial chips (Prolific PL2303 clones in particular) lock up internally
/// while staying fully enumerated, a condition a driver-binding cycle can't reach. Falls back to
/// disabling/re-enabling the PnP device node via <c>pnputil.exe</c> if the port-cycle path fails —
/// weaker, but still worth trying since it does fix a genuine driver-binding wedge.
/// </summary>
/// <remarks>
/// <para>
/// Tier 1 is not as strong as it looks: a hub only performs an actual VBUS power drop for
/// <c>IOCTL_USB_HUB_CYCLE_PORT</c> when its descriptor reports per-port switched power. Most
/// onboard root-hub ports and many external hubs report "ganged" or "no" power switching, in which
/// case Windows silently substitutes a USB bus reset instead — VBUS never actually drops. A bus
/// reset clears a protocol-level wedge but not every kind of internal silicon lockup, which is
/// exactly the gap between "software says it power-cycled the port" and "still needs a physical
/// unplug" that this class exists to close.
/// </para>
/// <para>
/// Tier 3 (new) escalates one level up the device tree: disable/re-enable the parent hub node
/// itself via <c>pnputil.exe</c>, only once both weaker tiers have already failed. Windows tears
/// down and fully re-powers every downstream port on a disabled hub regardless of that hub's
/// power-switching mode, so this reaches devices tier 1 cannot — at the cost of briefly dropping
/// every other device on the same hub (keyboard, mouse, printer, etc., if any share it). Reserved
/// for this last-resort path specifically because of that blast radius.
/// </para>
/// <para>
/// All three paths require an elevated (Administrator/SYSTEM) process. When not elevated, this
/// fails softly (returns false, logs the denial) and the caller falls back to the existing plain
/// reconnect/backoff behavior — no worse than before this feature existed.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsPnpSerialPortResetter : ISerialPortResetter
{
    public async Task<bool> TryResetAsync(string portName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        var instanceId = FindPnpInstanceId(portName);
        if (instanceId is null)
        {
            LifecycleLogger.Write("UsbSerialReset", $"deviceNotFound:{portName}");
            return false;
        }

        if (UsbHubPortCycler.TryCyclePort(instanceId))
        {
            LifecycleLogger.Write("UsbSerialReset", $"ok:hubCycle:{portName}");
            return true;
        }

        LifecycleLogger.Write("UsbSerialReset", $"hubCycleFailed:{portName}:fallingBackToPnpUtil");

        var disabled = await RunPnpUtilAsync("/disable-device", instanceId, cancellationToken).ConfigureAwait(false);
        if (disabled.Success)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

            var enabled = await RunPnpUtilAsync("/enable-device", instanceId, cancellationToken).ConfigureAwait(false);
            if (enabled.Success)
            {
                LifecycleLogger.Write("UsbSerialReset", $"ok:pnpUtil:{portName}");
                return true;
            }

            LifecycleLogger.Write("UsbSerialReset", $"enableFailed:{portName}:{enabled.Summary}");
        }
        else
        {
            LifecycleLogger.Write("UsbSerialReset", $"disableFailed:{portName}:{disabled.Summary}");
        }

        LifecycleLogger.Write("UsbSerialReset", $"pnpUtilFailed:{portName}:fallingBackToParentHubCycle");
        return await TryResetParentHubAsync(portName, instanceId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TryResetParentHubAsync(
        string portName, string instanceId, CancellationToken cancellationToken)
    {
        var parentInstanceId = UsbHubPortCycler.TryGetParentDeviceId(instanceId);
        if (parentInstanceId is null)
        {
            LifecycleLogger.Write("UsbSerialReset", $"parentHubNotFound:{portName}");
            return false;
        }

        var hubDisabled = await RunPnpUtilAsync("/disable-device", parentInstanceId, cancellationToken).ConfigureAwait(false);
        if (!hubDisabled.Success)
        {
            LifecycleLogger.Write("UsbSerialReset", $"parentHubDisableFailed:{portName}:{hubDisabled.Summary}");
            return false;
        }

        // A whole-hub power-down needs more settle time than a single device rebind before every
        // downstream port has actually gone cold.
        await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);

        var hubEnabled = await RunPnpUtilAsync("/enable-device", parentInstanceId, cancellationToken).ConfigureAwait(false);
        LifecycleLogger.Write(
            "UsbSerialReset",
            hubEnabled.Success ? $"ok:parentHubCycle:{portName}" : $"parentHubEnableFailed:{portName}:{hubEnabled.Summary}");
        return hubEnabled.Success;
    }

    private static string? FindPnpInstanceId(string portName)
    {
        try
        {
            using var serialPorts = new ManagementObjectSearcher(
                $"SELECT DeviceID, PNPDeviceID FROM Win32_SerialPort WHERE DeviceID = '{Escape(portName)}'");
            foreach (var device in serialPorts.Get())
            {
                if (device["PNPDeviceID"] is string id && !string.IsNullOrWhiteSpace(id))
                    return id;
            }

            // Fallback: some USB-to-serial drivers don't surface a Win32_SerialPort instance —
            // match the friendly "(COM3)" suffix on the generic PnP entity instead.
            using var pnpEntities = new ManagementObjectSearcher(
                $"SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%({Escape(portName)})'");
            foreach (var device in pnpEntities.Get())
            {
                if (device["PNPDeviceID"] is string id && !string.IsNullOrWhiteSpace(id))
                    return id;
            }
        }
        catch (ManagementException)
        {
            // WMI unavailable/misconfigured — treat as "device not found".
        }

        return null;
    }

    private static string Escape(string value) => value.Replace("'", "''");

    private static async Task<(bool Success, string Summary)> RunPnpUtilAsync(
        string action,
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pnputil.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(action);
            startInfo.ArgumentList.Add(instanceId);
            startInfo.ArgumentList.Add("/force");

            using var process = Process.Start(startInfo);
            if (process is null)
                return (false, "processStartFailed");

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode == 0)
                return (true, "ok");

            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            return (false, $"exit={process.ExitCode}:{detail.Trim()}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return (false, ex.Message);
        }
    }
}
