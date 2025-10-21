using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace XenDriverUtils {
    public class ThirdPartyStorageDriver {
        public string DriverInfPath { get; set; }
        public string Service { get; set; }
    }

    public class XenCleanup {
        static readonly IReadOnlyList<Guid> StorageClasses = new List<Guid>() {
            PInvoke.GUID_DEVCLASS_HDC,
            PInvoke.GUID_DEVCLASS_SCSIADAPTER,
        };

        public static List<ThirdPartyStorageDriver> Find3PStorageDrivers() {
            var found3PDrivers = new List<ThirdPartyStorageDriver>();
            foreach (var classGuid in StorageClasses) {
                var devInfo = PInvoke.SetupDiGetClassDevs(classGuid, null, HWND.Null, 0);
                foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)
                    .Where(x => DriverUtils.GetDeviceEnumeratorName(devInfo, x) == "PCI")) {
                    var driverPath = DriverUtils.GetDeviceDriverInfPath(devInfo, devInfoData);
                    if (driverPath.StartsWith("oem", StringComparison.OrdinalIgnoreCase)) {
                        found3PDrivers.Add(new ThirdPartyStorageDriver() {
                            DriverInfPath = driverPath,
                            Service = DriverUtils.GetDeviceService(devInfo, devInfoData),
                        });
                    }
                }
            }
            return found3PDrivers;
        }

        private static readonly List<Guid> XenfiltClasses = new() {
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

        public static void XenfiltClassCleanup() {
            using var classKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class", true);
            if (classKey == null) {
                return;
            }
            foreach (var classGuid in XenfiltClasses) {
                using var classSubkey = classKey.OpenSubKey(classGuid.ToString("B"), true);
                if (classSubkey == null) {
                    continue;
                }
                foreach (var filterValue in FilterValueList) {
                    try {
                        if (classSubkey.GetValueKind(filterValue) == RegistryValueKind.MultiString) {
                            var filters = (string[])classSubkey.GetValue(filterValue);
                            Logger.Log($"Class filters for {classGuid}: {string.Join(",", filters)}");
                            var newFilters = filters.Where(x => !FilterNameList
                                .Contains(x, StringComparer.OrdinalIgnoreCase))
                                .ToArray();
                            Logger.Log($"New filters for {classGuid}: {string.Join(",", newFilters)}");
                            classSubkey.SetValue(filterValue, newFilters, RegistryValueKind.MultiString);
                        }
                    } catch {
                    }
                }
            }
        }

        private static readonly List<string> OverridesToDelete = new() {
            "stornvme",
        };

        public static void ResetStartOverride() {
            try {
                foreach (var overrideName in OverridesToDelete.Concat(Find3PStorageDrivers().Select(x => x.Service))) {
                    Logger.Log($"Resetting {overrideName} StartOverride");
                    Registry.LocalMachine.DeleteSubKey(
                        $"SYSTEM\\CurrentControlSet\\Services\\{overrideName}\\StartOverride",
                        false);
                }
            } catch (Exception ex) {
                Logger.Log($"Cannot delete StartOverride subkey: {ex.Message}");
            }
        }

        private static readonly List<string> XenfiltParametersToDelete = new() {
            "ActiveDeviceID",
            "ActiveInstanceID",
            "ActiveLocationInformation",
        };

        public static void XenfiltReset() {
            using var paramKey = Registry.LocalMachine.OpenSubKey(
                "SYSTEM\\CurrentControlSet\\Services\\xenfilt\\Parameters",
                true);
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

        public static void ResetForceUnplug() {
            try {
                using var key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\XEN", true);
                if (key == null) {
                    return;
                }
                Logger.Log("Resetting Unplug key");
                key.DeleteSubKey("ForceUnplug");
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

        public static void DeleteService(CloseServiceHandleSafeHandle scm, string serviceName, bool stop = true) {
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

            if (stop) {
                if (PInvoke.ControlService(service, PInvoke.SERVICE_CONTROL_STOP, out var status)) {
                    Logger.Log($"Service {serviceName} stopped");
                } else {
                    Logger.Log($"ControlService({serviceName}, SERVICE_CONTROL_STOP) error {Marshal.GetLastWin32Error()}");
                }
            }

            if (PInvoke.DeleteService(service)) {
                Logger.Log($"Service {serviceName} deleted");
            } else {
                Logger.Log($"DeleteService({serviceName}) error {Marshal.GetLastWin32Error()}");
            }
        }
    }
}
