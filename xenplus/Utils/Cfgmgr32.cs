using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;
using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_GET_DEVICE_INTERFACE_LIST_FLAGS;

namespace XenPlus;

static class Cfgmgr32 {
    public static void CheckConfigret(CONFIGRET cr) {
        if (cr != CONFIGRET.CR_SUCCESS) {
            throw new Win32Exception(unchecked((int)PInvoke.CM_MapCrToWin32Err(cr, (uint)WIN32_ERROR.ERROR_GEN_FAILURE)));
        }
    }

    internal static IEnumerable<string> GetDeviceInterfaces(Guid interfaceClassGuid, string? deviceId = null) {
        CONFIGRET cr;
        char[] buf;
        do {
            unsafe {
                fixed (char* deviceIdPtr = deviceId) {
                    CheckConfigret(PInvoke.CM_Get_Device_Interface_List_Size(
                        out var len,
                        interfaceClassGuid,
                        deviceIdPtr,
                        CM_GET_DEVICE_INTERFACE_LIST_PRESENT));

                    buf = new char[len];
                    fixed (char* p = buf) {
                        cr = PInvoke.CM_Get_Device_Interface_List(
                            interfaceClassGuid,
                            deviceIdPtr,
                            p,
                            (uint)buf.Length,
                            CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
                    }
                }
            }
        } while (cr == CONFIGRET.CR_BUFFER_SMALL);
        CheckConfigret(cr);

        foreach (var device in ServerUtils.ParseMultiString(buf)) {
            yield return device;
        }
    }

    internal static IEnumerable<string> GetDeviceInstances(string? filter, uint flags) {
        CONFIGRET cr;
        char[] buf;
        do {
            CheckConfigret(PInvoke.CM_Get_Device_ID_List_Size(
                out var len,
                filter,
                flags));

            buf = new char[len];
            unsafe {
                fixed (char* p = buf) {
                    cr = PInvoke.CM_Get_Device_ID_List(filter, p, (uint)buf.Length, flags);
                }
            }
        } while (cr == CONFIGRET.CR_BUFFER_SMALL);
        CheckConfigret(cr);

        foreach (var device in ServerUtils.ParseMultiString(buf)) {
            yield return device;
        }
    }

    internal static List<string> GetChildren(uint inst) {
        char[]? buffer = null;
        uint len;
        while (true) {
            unsafe {
                len = (uint)(buffer?.Length ?? 0) * sizeof(char);
                var cr = PInvoke.CM_Get_DevNode_Property(
                    inst,
                    PInvoke.DEVPKEY_Device_Children,
                    out var devPropType,
                    MemoryMarshal.AsBytes(buffer.AsSpan()),
                    ref len,
                    0);

                if (cr == CONFIGRET.CR_SUCCESS) {
                    Check.Assert(devPropType == DEVPROPTYPE.DEVPROP_TYPE_STRING_LIST);
                    break;
                } else if (cr != CONFIGRET.CR_BUFFER_SMALL) {
                    CheckConfigret(cr);
                }
                if (len % 2 == 1) {
                    len++;
                }
                buffer = new char[len / sizeof(char)];
            }
        }
        if (buffer == null) {
            return [];
        }

        return ServerUtils.ParseMultiString(buffer);
    }
}
