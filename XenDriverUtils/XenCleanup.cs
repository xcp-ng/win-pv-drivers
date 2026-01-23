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
                using var devInfo = PInvoke.SetupDiGetClassDevs(classGuid, null, HWND.Null, 0);
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

        public static void XenfiltClassCleanup(bool dryRun) {
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
                            Logger.LogFormat(LogLevel.Info, "Class filters for {0}: {1}", classGuid, string.Join(",", filters));

                            var newFilters = filters.Where(x => !FilterNameList
                                .Contains(x, StringComparer.OrdinalIgnoreCase))
                                .ToArray();
                            Logger.LogFormat(LogLevel.Info, "New filters for {0}: {1}", classGuid, string.Join(",", newFilters));

                            if (!dryRun) {
                                classSubkey.SetValue(filterValue, newFilters, RegistryValueKind.MultiString);
                            }
                        }
                    } catch {
                    }
                }
            }
        }

        private static readonly List<string> OverridesToDelete = new() {
            "stornvme",
        };

        public static void ResetStartOverride(bool dryRun) {
            try {
                foreach (var overrideName in OverridesToDelete.Concat(Find3PStorageDrivers().Select(x => x.Service))) {
                    Logger.Log($"Resetting {overrideName} StartOverride");
                    if (!dryRun) {
                        Registry.LocalMachine.DeleteSubKey(
                            $"SYSTEM\\CurrentControlSet\\Services\\{overrideName}\\StartOverride",
                            false);
                    }
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

        public static void XenfiltReset(bool dryRun) {
            using var paramKey = Registry.LocalMachine.OpenSubKey(
                "SYSTEM\\CurrentControlSet\\Services\\xenfilt\\Parameters",
                true);
            if (paramKey == null) {
                return;
            }
            foreach (var paramName in XenfiltParametersToDelete) {
                try {
                    if (!dryRun) {
                        paramKey.DeleteValue(paramName);
                    }
                    Logger.Log($"Deleted xenfilt parameter {paramName}");
                } catch {
                }
            }
        }

        public static void ResetUnplug(bool dryRun) {
            try {
                using var key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\XEN", true);
                if (key == null) {
                    return;
                }
                Logger.Log("Resetting Unplug key");
                if (!dryRun) {
                    key.DeleteSubKey("Unplug");
                }
            } catch {
            }
        }

        public static void ResetForceUnplug(bool dryRun) {
            try {
                using var key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\XEN", true);
                if (key == null) {
                    return;
                }
                Logger.Log("Resetting Unplug key");
                if (!dryRun) {
                    key.DeleteSubKey("ForceUnplug");
                }
            } catch {
            }
        }

        // (ServiceName, IsDriverService)
        // other code may want to use this list too, so make it public
        public static readonly IReadOnlyList<Tuple<string, bool>> DeleteableServices = new List<Tuple<string, bool>>() {
            new("xenagent", true),
            new("xenbus", true),
            new("xenbus_monitor", true),
            new("xencons", true),
            new("xencons_monitor", true),
            new("xendisk", true),
            new("xenfilt", true),
            new("xenhid", true),
            new("xeniface", true),
            new("xennet", true),
            new("xenvbd", true),
            new("xenvif", true),
            new("xenvkbd", true),
            new("XenInstall", false),
            new("XenSvc", false),
        };

        public static void DeleteService(CloseServiceHandleSafeHandle scm, string serviceName, bool dryRun, bool stop = true) {
            if (!DeleteableServices.Any(x => string.Equals(x.Item1, serviceName, StringComparison.OrdinalIgnoreCase))) {
                Logger.Log($"Refusing to delete service {serviceName}");
                return;
            }
            Logger.LogFormat(LogLevel.Info, "Deleting service {0}", serviceName);

            using var service = PInvoke.OpenService(scm, serviceName, PInvoke.SERVICE_ALL_ACCESS);
            if (service.IsInvalid) {
                var err = Marshal.GetLastWin32Error();
                if (err == (int)WIN32_ERROR.ERROR_SERVICE_DOES_NOT_EXIST) {
                    Logger.LogFormat(LogLevel.Info, "Service {0} does not exist", serviceName);
                } else {
                    Logger.LogFormat(LogLevel.Interactive, "OpenService({0}) error {1}", serviceName, err);
                }
                return;
            }

            if (!dryRun) {
                if (stop) {
                    if (PInvoke.ControlService(service, PInvoke.SERVICE_CONTROL_STOP, out var status)) {
                        Logger.LogFormat(LogLevel.Interactive, "Stopped service {0}", serviceName);
                    } else {
                        Logger.LogFormat(
                            LogLevel.Alert,
                            "ControlService({0}, SERVICE_CONTROL_STOP) error {1}",
                            serviceName,
                            Marshal.GetLastWin32Error());
                    }
                }

                if (PInvoke.DeleteService(service)) {
                    Logger.LogFormat(LogLevel.Interactive, "Deleted service {0}", serviceName);
                } else {
                    Logger.LogFormat(
                        LogLevel.Alert,
                        "DeleteService({0}) error {1}",
                        serviceName,
                        Marshal.GetLastWin32Error());
                }
            }
        }

        public static bool IsSafeMode() {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAFEBOOT_OPTION"));
        }
    }
}
