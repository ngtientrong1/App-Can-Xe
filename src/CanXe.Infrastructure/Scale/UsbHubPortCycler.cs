using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using CanXe.Infrastructure.Logging;
using Microsoft.Win32.SafeHandles;

namespace CanXe.Infrastructure.Scale;

/// <summary>
/// Power-cycles the exact USB hub port a device is attached to, via the same
/// <c>IOCTL_USB_HUB_CYCLE_PORT</c> mechanism Windows' own USB stack uses internally — this drops
/// and restores the port's VBUS power, which is what a physical unplug/replug actually does.
/// This is meaningfully different from (and stronger than) <c>pnputil /disable-device</c>, which
/// only unbinds/rebinds the driver without touching bus power at all — the right tool for a device
/// that's wedged at the Windows driver-binding level, but useless against a USB-serial chip whose
/// own internal UART logic has locked up while remaining fully enumerated.
/// </summary>
[SupportedOSPlatform("windows")]
public static class UsbHubPortCycler
{
    private static readonly Guid GuidDevinterfaceUsbHub = new("F18A0E88-C30C-11D0-8815-00A0C906BED8");

    private const uint CrSuccess = 0;
    private const uint CmLocateDevnodeNormal = 0;
    private const uint CmDrpAddress = 0x1D;
    private const uint CmGetDeviceInterfaceListPresent = 0;
    private const uint IoctlUsbHubCyclePort = 0x220444;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    /// <summary>
    /// Resolves the PNPDeviceID of the USB hub <paramref name="pnpDeviceId"/> is directly attached
    /// to. Used as a last-resort escalation: on hubs that report "ganged" or "no" per-port power
    /// switching in their hub descriptor, <see cref="TryCyclePort"/>'s IOCTL can only fall back to a
    /// USB bus reset (VBUS stays up the whole time) instead of an actual power drop — which does not
    /// clear every kind of silicon-level lockup a physical unplug does. Disabling/re-enabling the
    /// whole parent hub node instead forces Windows to tear down and fully re-power every downstream
    /// port, including ours, regardless of that hub's power-switching capability.
    /// </summary>
    public static string? TryGetParentDeviceId(string pnpDeviceId)
    {
        try
        {
            if (CM_Locate_DevNodeW(out var devInst, pnpDeviceId, CmLocateDevnodeNormal) != CrSuccess)
                return null;
            if (CM_Get_Parent(out var parentDevInst, devInst, 0) != CrSuccess)
                return null;
            return GetDeviceId(parentDevInst);
        }
        catch (Exception ex)
        {
            LifecycleLogger.Write("UsbHubCycle", $"parentLookupError:{ex.GetType().Name}:{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to cycle the USB port that <paramref name="pnpDeviceId"/> (the target device's
    /// PNPDeviceID, e.g. resolved via WMI from a COM port name) is attached to. Returns false —
    /// never throws — for anything short of a confirmed successful IOCTL, so callers can fall back
    /// to a weaker recovery path.
    /// </summary>
    public static bool TryCyclePort(string pnpDeviceId)
    {
        if (string.IsNullOrWhiteSpace(pnpDeviceId))
            return false;

        try
        {
            if (CM_Locate_DevNodeW(out var devInst, pnpDeviceId, CmLocateDevnodeNormal) != CrSuccess)
            {
                LifecycleLogger.Write("UsbHubCycle", "locateDevNodeFailed");
                return false;
            }

            if (!TryGetPortAddress(devInst, out var portAddress))
            {
                LifecycleLogger.Write("UsbHubCycle", "portAddressUnavailable");
                return false;
            }

            if (CM_Get_Parent(out var parentDevInst, devInst, 0) != CrSuccess)
            {
                LifecycleLogger.Write("UsbHubCycle", "parentNotFound");
                return false;
            }

            var parentDeviceId = GetDeviceId(parentDevInst);
            if (parentDeviceId is null)
            {
                LifecycleLogger.Write("UsbHubCycle", "parentIdUnavailable");
                return false;
            }

            var hubInterfacePath = GetHubInterfacePath(parentDeviceId);
            if (hubInterfacePath is null)
            {
                LifecycleLogger.Write("UsbHubCycle", $"hubInterfaceNotFound:{parentDeviceId}");
                return false;
            }

            using var hubHandle = CreateFileW(
                hubInterfacePath, GenericWrite, FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (hubHandle.IsInvalid)
            {
                LifecycleLogger.Write("UsbHubCycle", $"hubOpenFailed:{Marshal.GetLastWin32Error()}");
                return false;
            }

            var cycleParams = new UsbCyclePortParams { ConnectionIndex = portAddress };
            var size = Marshal.SizeOf<UsbCyclePortParams>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(cycleParams, buffer, false);
                var ok = DeviceIoControl(
                    hubHandle, IoctlUsbHubCyclePort, buffer, (uint)size, buffer, (uint)size,
                    out _, IntPtr.Zero);
                if (!ok)
                {
                    LifecycleLogger.Write("UsbHubCycle", $"ioctlFailed:{Marshal.GetLastWin32Error()}");
                    return false;
                }

                LifecycleLogger.Write("UsbHubCycle", $"ok:port={portAddress}");
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            LifecycleLogger.Write("UsbHubCycle", $"error:{ex.GetType().Name}:{ex.Message}");
            return false;
        }
    }

    private static bool TryGetPortAddress(uint devInst, out uint portAddress)
    {
        portAddress = 0;
        uint length = sizeof(uint);
        var buffer = new byte[length];
        if (CM_Get_DevNode_Registry_PropertyW(devInst, CmDrpAddress, out _, buffer, ref length, 0) != CrSuccess)
            return false;

        portAddress = BitConverter.ToUInt32(buffer, 0);
        return portAddress > 0;
    }

    private static string? GetDeviceId(uint devInst)
    {
        var buffer = new StringBuilder(512);
        return CM_Get_Device_IDW(devInst, buffer, (uint)buffer.Capacity, 0) == CrSuccess
            ? buffer.ToString()
            : null;
    }

    private static string? GetHubInterfacePath(string hubDeviceId)
    {
        var guid = GuidDevinterfaceUsbHub;
        if (CM_Get_Device_Interface_List_SizeW(out var length, ref guid, hubDeviceId, CmGetDeviceInterfaceListPresent) != CrSuccess
            || length <= 1)
            return null;

        var buffer = new char[length];
        if (CM_Get_Device_Interface_ListW(ref guid, hubDeviceId, buffer, length, CmGetDeviceInterfaceListPresent) != CrSuccess)
            return null;

        // Result is a REG_MULTI_SZ (double-null-terminated list of null-terminated strings) — take the first.
        var text = new string(buffer);
        var end = text.IndexOf('\0');
        return end > 0 ? text[..end] : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UsbCyclePortParams
    {
        public uint ConnectionIndex;
        public uint StatusReturned;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_IDW(uint dnDevInst, StringBuilder buffer, uint bufferLen, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_Registry_PropertyW(
        uint dnDevInst, uint ulProperty, out uint pulRegDataType, byte[] buffer, ref uint pulLength, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_Interface_List_SizeW(
        out uint pulLen, ref Guid interfaceClassGuid, string pDeviceID, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_Interface_ListW(
        ref Guid interfaceClassGuid, string pDeviceID, [Out] char[] buffer, uint bufferLen, uint ulFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize,
        IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
}
