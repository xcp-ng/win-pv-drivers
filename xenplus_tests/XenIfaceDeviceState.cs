using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;

using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_LOCATE_DEVNODE_FLAGS;

namespace XenPlus;

/// <summary>
/// Changes device state through CfgMgr32 rather than SetupAPI DICS_DISABLE: the default class installer turns a
/// query-remove veto into a disable deferred to the next boot and reports DI_NEEDREBOOT, so the refusal cannot be
/// observed and leaves the device disabled after a reboot. Without CM_DISABLE_PERSIST, a disable lasts only until
/// the next boot, so nothing outlives the boot even if a test aborts before re-enabling the device.
/// </summary>
static class XenIfaceDeviceState {
    static unsafe uint Locate(string instanceId) {
        uint devInst;
        fixed (char* id = instanceId) {
            // Reuse xenplus's CsWin32 declaration through IVT.
            Cfgmgr32.CheckConfigret(PInvoke.CM_Locate_DevNode(&devInst, id, CM_LOCATE_DEVNODE_NORMAL));
        }
        return devInst;
    }

    /// <summary>
    /// Disable the device until the next boot. The call blocks until Windows has removed the device, and returns
    /// CR_REMOVE_VETOED when query-remove is vetoed.
    /// </summary>
    public static CONFIGRET Disable(string instanceId) {
        // Report a veto to the caller instead of displaying it.
        return PInvokeT.CM_Disable_DevNode(Locate(instanceId), PInvokeT.CM_DISABLE_UI_NOT_OK);
    }

    public static void Enable(string instanceId) {
        Cfgmgr32.CheckConfigret(PInvokeT.CM_Enable_DevNode(Locate(instanceId), 0));
    }
}
