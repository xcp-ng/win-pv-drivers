using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;
using WixToolset.Dtf.WindowsInstaller;

namespace XenInstCA {
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

        public static readonly DEVPROPKEY DEVPKEY_Device_DriverInfPath = new() {
            fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6),
            pid = 5
        };

        public static readonly DEVPROPKEY DEVPKEY_Device_Children = new() {
            fmtid = new Guid(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7),
            pid = 9
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
                    var err = Marshal.GetLastWin32Error();
                    if ((WIN32_ERROR)err == WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER) {
                        ; // expected error, continue to next step
                    } else if ((WIN32_ERROR)err == WIN32_ERROR.ERROR_NOT_FOUND) {
                        // expected error, device doesn't have property
                        return null;
                    } else {
                        return null;
                    }
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
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
            }
            return buf;
        }

        public static List<string> GetDeviceChildren(SetupDiDestroyDeviceInfoListSafeHandle devInfo, SP_DEVINFO_DATA devInfoData) {
            var buf = DriverUtils.GetDeviceProperty<char>(
                devInfo,
                devInfoData,
                DriverUtils.DEVPKEY_Device_Children,
                DEVPROPTYPE.DEVPROP_TYPE_STRING_LIST);
            if (buf == null) {
                return new List<string>();
            }
            return DriverUtils.ParseMultiString(buf);
        }

        public static List<string> GetDeviceCompatibleIds(SetupDiDestroyDeviceInfoListSafeHandle devInfo, SP_DEVINFO_DATA devInfoData) {
            var buf = DriverUtils.GetDeviceProperty<char>(
                devInfo,
                devInfoData,
                DriverUtils.DEVPKEY_Device_CompatibleIds,
                DEVPROPTYPE.DEVPROP_TYPE_STRING_LIST);
            if (buf == null) {
                return new List<string>();
            }
            return DriverUtils.ParseMultiString(buf);
        }

        public static string GetDeviceInfPath(SetupDiDestroyDeviceInfoListSafeHandle devInfo, SP_DEVINFO_DATA devInfoData) {
            var buf = DriverUtils.GetDeviceProperty<char>(
                devInfo,
                devInfoData,
                DriverUtils.DEVPKEY_Device_DriverInfPath,
                DEVPROPTYPE.DEVPROP_TYPE_STRING_LIST);
            if (buf == null) {
                return null;
            }
            // we don't know the actual length of the string returned by GetDeviceProperty
            var infPath = new StringBuilder();
            foreach (var ch in buf) {
                if (ch == 0) {
                    break;
                }
                infPath.Append(ch);
            }
            return infPath.ToString();
        }

        public static IEnumerable<SP_DEVINFO_DATA> EnumerateDevices(SetupDiDestroyDeviceInfoListSafeHandle devInfo) {
            var devInfoData = new SP_DEVINFO_DATA {
                cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
            };
            for (uint devIndex = 0; ; devIndex++) {
                if (!PInvoke.SetupDiEnumDeviceInfo(devInfo, devIndex, ref devInfoData)) {
                    var error = Marshal.GetLastWin32Error();
                    if ((WIN32_ERROR)error == WIN32_ERROR.ERROR_NO_MORE_ITEMS) {
                        yield break;
                    }
                }
                yield return devInfoData;
            }
        }

        public static bool DiRemoveDevice(SetupDiDestroyDeviceInfoListSafeHandle devInfo, SP_DEVINFO_DATA devInfoData, out bool needsReboot) {
            if (!PInvoke.SetupDiCallClassInstaller(DI_FUNCTION.DIF_REMOVE, devInfo, devInfoData)) {
                needsReboot = false;
                return false;
            }
            var devInstallParams = new SP_DEVINSTALL_PARAMS_W() { cbSize = (uint)Marshal.SizeOf<SP_DEVINSTALL_PARAMS_W>() };
            if (!PInvoke.SetupDiGetDeviceInstallParams(devInfo, devInfoData, ref devInstallParams)) {
                needsReboot = false;
                return false;
            }
            needsReboot = (devInstallParams.Flags & (SETUP_DI_DEVICE_INSTALL_FLAGS.DI_NEEDREBOOT | SETUP_DI_DEVICE_INSTALL_FLAGS.DI_NEEDRESTART)) != 0;
            return true;
        }
    }
}
