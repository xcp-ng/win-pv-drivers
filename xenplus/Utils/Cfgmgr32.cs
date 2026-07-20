using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;

using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_GET_DEVICE_INTERFACE_LIST_FLAGS;

namespace XenPlus;

static class Cfgmgr32 {
    internal static IEnumerable<string> GetDeviceInterfaces(Guid interfaceClassGuid) {
        CONFIGRET cr;
        char[] buf;
        do {
            ServerUtils.CheckConfigret(PInvoke.CM_Get_Device_Interface_List_Size(
                out var len,
                interfaceClassGuid,
                null,
                CM_GET_DEVICE_INTERFACE_LIST_PRESENT));

            buf = new char[len];
            unsafe {
                fixed (char* p = buf) {
                    cr = PInvoke.CM_Get_Device_Interface_List(
                        interfaceClassGuid,
                        null,
                        p,
                        (uint)buf.Length,
                        CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
                }
            }
        } while (cr == CONFIGRET.CR_BUFFER_SMALL);
        ServerUtils.CheckConfigret(cr);

        foreach (var device in ServerUtils.ParseMultiString(buf)) {
            yield return device;
        }
    }
}
