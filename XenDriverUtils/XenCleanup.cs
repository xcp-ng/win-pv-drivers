using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;

namespace XenDriverUtils {
    public class XenCleanup {
        private static readonly List<Guid> ClassKeyList = new() {
            PInvoke.GUID_DEVCLASS_HDC,
            PInvoke.GUID_DEVCLASS_SYSTEM,
        };

        private static readonly List<string> FilterValueList = new() {
            "LowerFilters",
            "UpperFilters",
        };

        private static readonly List<string> FilterNameList = new() {
            "xenfilt",
            "scsifilt",
        };

        public static void XenbusCleanup() {
            using var classKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class", true);
            if (classKey == null) {
                return;
            }
            foreach (var classGuid in ClassKeyList) {
                using var classSubkey = classKey.OpenSubKey(classGuid.ToString("B"), true);
                if (classSubkey == null) {
                    continue;
                }
                foreach (var filterValue in FilterValueList) {
                    try {
                        if (classSubkey.GetValueKind(filterValue) == RegistryValueKind.MultiString) {
                            var filters = (string[])classSubkey.GetValue(filterValue);
                            Logger.Log($"Class filters for {classGuid}: {string.Join(",", filters)}");
                            var newFilters = filters.Where(x => !FilterNameList.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
                            Logger.Log($"New filters for {classGuid}: {string.Join(",", newFilters)}");
                            classSubkey.SetValue(filterValue, newFilters, RegistryValueKind.MultiString);
                        }
                    } catch {
                    }
                }
            }
        }

        public static void ResetNvmeOverride() {
            try {
                using var key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\stornvme", true);
                if (key == null) {
                    return;
                }
                Logger.Log("Resetting stornvme StartOverride");
                key.DeleteSubKey("StartOverride");
            } catch {
            }
        }

        private static readonly List<string> XenfiltParametersToDelete = new() {
            "ActiveDeviceID",
            "ActiveInstanceID",
            "ActiveLocationInformation",
        };

        public static void XenfiltReset() {
            using var paramKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\xenfilt\\Parameters", true);
            if (paramKey == null) {
                return;
            }
            foreach (var paramName in XenfiltParametersToDelete) {
                try {
                    paramKey.DeleteValue(paramName);
                    Logger.Log($"Deleted xenfilt parameter {paramName}");
                } catch {
                }
            }
        }

        public static void ResetUnplug() {
            try {
                using var key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\XEN", true);
                if (key == null) {
                    return;
                }
                Logger.Log("Resetting Unplug key");
                key.DeleteSubKey("Unplug");
            } catch {
            }
        }

        // other code may want to use this list too, so make it public
        public static readonly List<string> DeleteableServices = new() {
            "xenagent",
            "xenbus",
            "xenbus_monitor",
            "xencons",
            "xencons_monitor",
            "xendisk",
            "xenfilt",
            "xenhid",
            "xeniface",
            "XenInstall",
            "xennet",
            "XenSvc",
            "xenvbd",
            "xenvif",
            "xenvkbd",
        };

        public static void DeleteService(CloseServiceHandleSafeHandle scm, string serviceName) {
            if (!DeleteableServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase)) {
                Logger.Log($"Refusing to delete service {serviceName}");
                return;
            }
            Logger.Log($"Deleting service {serviceName}");

            using var service = PInvoke.OpenService(scm, serviceName, PInvoke.SERVICE_ALL_ACCESS);
            if (service.IsInvalid) {
                Logger.Log($"OpenService {serviceName} error {Marshal.GetLastWin32Error()}");
                return;
            }

            if (PInvoke.ControlService(service, PInvoke.SERVICE_CONTROL_STOP, out var status)) {
                Logger.Log($"Service {serviceName} stopped");
            } else {
                Logger.Log($"ControlService({serviceName}, SERVICE_CONTROL_STOP) error {Marshal.GetLastWin32Error()}");
            }

            if (PInvoke.DeleteService(service)) {
                Logger.Log($"Service {serviceName} deleted");
            } else {
                Logger.Log($"DeleteService({serviceName}) error {Marshal.GetLastWin32Error()}");
            }
        }
    }
}
