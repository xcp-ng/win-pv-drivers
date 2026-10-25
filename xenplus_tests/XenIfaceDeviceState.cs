using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

using static Windows.Win32.Devices.DeviceAndDriverInstallation.DI_FUNCTION;
using static Windows.Win32.Devices.DeviceAndDriverInstallation.SETUP_DI_DEVICE_INSTALL_FLAGS;
using static Windows.Win32.Devices.DeviceAndDriverInstallation.SETUP_DI_PROPERTY_CHANGE_SCOPE;
using static Windows.Win32.Devices.DeviceAndDriverInstallation.SETUP_DI_STATE_CHANGE;

namespace XenPlus;

static class XenIfaceDeviceState {
    /// <summary>
    /// Restore the enabled configuration using SetupAPI. Success can still require a system reboot.
    /// </summary>
    public static unsafe bool Enable(string instanceId) {
        var nativeInfo = PInvokeT.SetupDiCreateDeviceInfoList((Guid*)null, HWND.Null);
        using var info = new SetupDiDestroyDeviceInfoListSafeHandle(nativeInfo.Value, ownsHandle: true);
        if (info.IsInvalid) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), nameof(PInvokeT.SetupDiCreateDeviceInfoList));
        }

        var device = new SP_DEVINFO_DATA {
            cbSize = (uint)sizeof(SP_DEVINFO_DATA)
        };
        fixed (char* id = instanceId) {
            if (!PInvokeT.SetupDiOpenDeviceInfo(nativeInfo, id, HWND.Null, 0, &device)) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), nameof(PInvokeT.SetupDiOpenDeviceInfo));
            }
        }

        // Enable globally before enabling in the current hardware profile, following Microsoft's DevCon sample:
        // https://github.com/microsoft/Windows-driver-samples/blob/main/setup/devcon/cmds.cpp (ControlCallback).
        var rebootRequired = false;
        try {
            rebootRequired = EnableInScope(info, nativeInfo, ref device, DICS_FLAG_GLOBAL);
        } catch (Win32Exception ex) {
            // DevCon also continues to the current-profile enable when the global attempt fails.
            TestContext.Current.TestOutputHelper?.WriteLine($"Global enable attempt failed: {ex.Message}");
        }
        rebootRequired |= EnableInScope(info, nativeInfo, ref device, DICS_FLAG_CONFIGSPECIFIC);
        return rebootRequired;
    }

    static unsafe bool EnableInScope(
        SetupDiDestroyDeviceInfoListSafeHandle info,
        HDEVINFO nativeInfo,
        ref SP_DEVINFO_DATA device,
        SETUP_DI_PROPERTY_CHANGE_SCOPE scope) {

        var change = new SP_PROPCHANGE_PARAMS {
            ClassInstallHeader = new SP_CLASSINSTALL_HEADER {
                cbSize = (uint)sizeof(SP_CLASSINSTALL_HEADER),
                InstallFunction = DIF_PROPERTYCHANGE
            },
            StateChange = DICS_ENABLE,
            Scope = scope,
            HwProfile = 0
        };
        fixed (SP_DEVINFO_DATA* devicePointer = &device) {
            if (!PInvokeT.SetupDiSetClassInstallParams(
                nativeInfo, devicePointer, &change.ClassInstallHeader, (uint)sizeof(SP_PROPCHANGE_PARAMS))) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), $"SetupDiSetClassInstallParams ({scope})");
            }
            if (!PInvokeT.SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, nativeInfo, devicePointer)) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), $"SetupDiCallClassInstaller ({scope})");
            }
        }

        var install = new SP_DEVINSTALL_PARAMS_W {
            cbSize = (uint)Unsafe.SizeOf<SP_DEVINSTALL_PARAMS_W>()
        };
        if (!PInvokeT.SetupDiGetDeviceInstallParams(info, device, ref install)) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), nameof(PInvokeT.SetupDiGetDeviceInstallParams));
        }
        return (install.Flags & (DI_NEEDREBOOT | DI_NEEDRESTART)) != 0;
    }
}
