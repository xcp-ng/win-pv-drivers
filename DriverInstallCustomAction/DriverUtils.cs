using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using WixToolset.Dtf.WindowsInstaller;

namespace XNInstCA {
    public class DriverData {
        public string DriverName { get; set; }
        public string InfPath { get; set; }
    }

    internal static class DriverUtils {
        public static DriverData GetDriverData(Session session) {
            if (!session.CustomActionData.TryGetValue("Driver", out var driverName)) return null;
            if (!session.CustomActionData.TryGetValue("Inf", out var infPath)) return null;
            return new DriverData() {
                DriverName = driverName,
                InfPath = infPath,
            };
        }

        public static List<string> ParseMultiString(char[] buf) {
            var strings = new List<string>();
            int first = 0;
            for (int i = 0; i < buf.Length; i++) {
                if (buf[i] == '\0') {
                    strings.Add(new string(buf, first, i - first));
                    first = i + 1;
                }
            }
            if (strings.Last() == "") {
                strings.RemoveAt(strings.Count - 1);
            }
            return strings;
        }

        public static readonly DEVPROPKEY DEVPKEY_Device_CompatibleIds = new() {
            fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
            pid = 4
        };

        public static T[] GetDeviceProperty<T>(
                SetupDiDestroyDeviceInfoListSafeHandle devInfo,
                SP_DEVINFO_DATA devInfoData,
                DEVPROPKEY propKey,
                DEVPROPTYPE expectedType) where T : unmanaged {
            uint requiredBytes;
            long numElements;
            unsafe {
                if (!PInvoke.SetupDiGetDeviceProperty(
                        devInfo,
                        devInfoData,
                        propKey,
                        out var ptype,
                        Span<byte>.Empty,
                        &requiredBytes,
                        0)
                    || ptype != expectedType
                    || requiredBytes < sizeof(T)
                    || requiredBytes % sizeof(T) != 0) {
                    return null;
                }
                numElements = requiredBytes / sizeof(T);
            }
            var buf = new T[numElements];
            unsafe {
                fixed (T* p = buf) {
                    if (!PInvoke.SetupDiGetDeviceProperty(
                            devInfo,
                            devInfoData,
                            propKey,
                            out _,
                            new Span<byte>(p, (int)requiredBytes),
                            null,
                            0)) {
                        return null;
                    }
                }
            }
            return buf;
        }
    }
}
