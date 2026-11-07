using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

namespace XenDriverUtils {
    public class ThirdPartyStorageDriver {
        public string DriverInfPath { get; }
        public string Service { get; }

        public ThirdPartyStorageDriver(string driverInfPath, string service) {
            DriverInfPath = driverInfPath;
            Service = service;
        }
    }

    public enum UnplugType {
        Disks,
        Nics,
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
                    var service = DriverUtils.GetDeviceService(devInfo, devInfoData);
                    if (driverPath?.StartsWith("oem", StringComparison.OrdinalIgnoreCase) == true
                            && !string.IsNullOrEmpty(service)) {
                        found3PDrivers.Add(new ThirdPartyStorageDriver(driverPath, service!));
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
                            var filters = classSubkey.GetValue(filterValue) as string[];
                            if (filters == null) {
                                continue;
                            }
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
                Logger.LogFormat(LogLevel.Interactive, "Cannot delete StartOverride subkey: {0}", ex.ToString());
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

        static readonly IReadOnlyList<string> UnplugKeys = new List<string>() {
            "Unplug",
            "ForceUnplug",
        };

        public static void ResetAllUnplug(bool dryRun) {
            try {
                using var key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\XEN", true);
                if (key == null) {
                    return;
                }
                foreach (var unplugKey in UnplugKeys) {
                    Logger.LogFormat(LogLevel.Interactive, "Resetting {0}", unplugKey);
                    if (!dryRun) {
                        key.DeleteSubKey(unplugKey);
                    }
                }
            } catch {
            }
        }

        public static void ResetUnplug(UnplugType unplugType, bool dryRun) {
            try {
                foreach (var unplugKey in UnplugKeys) {
                    using var key = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\XEN\\{unplugKey}", true);
                    if (key == null) {
                        return;
                    }
                    Logger.LogFormat(LogLevel.Interactive, "Resetting {0} for type {1}", unplugKey, unplugType);
                    if (!dryRun) {
                        switch (unplugType) {
                            case UnplugType.Disks:
                                key.DeleteValue("DISKS");
                                break;
                            case UnplugType.Nics:
                                key.DeleteValue("NICS");
                                break;
                        }
                    }
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

        static readonly IReadOnlyList<string> DeviceUninstallOrder = new List<string>() {
            "Xenvbd",
            "Xennet",
            "Xenvif",
            "Xenhid",
            "Xenvkbd",
            "Xencons",
            "Xeniface",
            "Xendevice",
            "Xenclass",
            "Xenbus",
        };

        static bool UninstallDevices(XenDeviceInfo xenInfo, bool dryRun) {
            using var devInfo = PInvoke.SetupDiGetClassDevs(
                xenInfo.ClassGuid,
                null,
                HWND.Null,
                xenInfo.ClassGuid.HasValue ? 0 : SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES);
            bool needsReboot = false;

            foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                List<string> compatibleIds = DriverUtils.GetDeviceHardwareAndCompatibleIds(devInfo, devInfoData);
                if (compatibleIds.All(x => !xenInfo.MatchesId(x, checkKnown: true, checkIncompatible: true))) {
                    continue;
                }

                var instanceId = DriverUtils.GetDeviceInstanceId(devInfo, devInfoData);
                if (instanceId != null) {
                    Logger.Log($"Found device: {instanceId}");
                } else {
                    Logger.Log($"Found device");
                }

                Logger.LogFormat(LogLevel.Interactive, "Uninstalling device {0}", instanceId ?? "");
                if (!dryRun) {
                    try {
                        DriverUtils.UninstallDevice(devInfo, devInfoData, out var thisNeedsReboot);
                        needsReboot |= thisNeedsReboot;
                    } catch (Exception ex) {
                        Logger.LogFormat(LogLevel.Interactive, "Cannot uninstall device: {0}", ex.ToString());
                    }
                }
            }

            return needsReboot;
        }

        public static bool UninstallAllDevices(bool dryRun) {
            bool needsReboot = false;

            foreach (var driver in DeviceUninstallOrder) {
                var xenInfo = XenDeviceInfo.KnownDevices[driver];
                Logger.LogFormat(LogLevel.Info, "Finding driver {0}", driver);
                needsReboot |= UninstallDevices(xenInfo, dryRun: dryRun);
            }

            Logger.LogFormat(LogLevel.Info, "Device removal needs reboot: {0}", needsReboot);
            return needsReboot;
        }

        public static bool UninstallDriver(string driverName, bool dryRun) {
            bool needsReboot = false;

            if (!XenDeviceInfo.KnownDevices.TryGetValue(driverName, out var xenInfo)) {
                Logger.Log($"Unknown driver {driverName}");
                return needsReboot;
            }

            var collectedInfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var devInfo = PInvoke.SetupDiGetClassDevs(
                xenInfo.ClassGuid,
                null,
                HWND.Null,
                xenInfo.ClassGuid.HasValue ? 0 : SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES)) {
                foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                    List<string> hardwareIds = DriverUtils.GetDeviceHardwareAndCompatibleIds(devInfo, devInfoData);
                    if (hardwareIds.All(x => !xenInfo.MatchesId(x, checkKnown: true, checkIncompatible: false))) {
                        continue;
                    }

                    var instanceId = DriverUtils.GetDeviceInstanceId(devInfo, devInfoData);
                    if (instanceId != null) {
                        Logger.Log($"Found {driverName} device: {instanceId}");
                    } else {
                        Logger.Log($"Found {driverName} device");
                    }

                    var infName = DriverUtils.GetDeviceDriverInfPath(devInfo, devInfoData);
                    Logger.Log($"Current inf path: {infName}");
                    if (!string.IsNullOrEmpty(infName)
                            && infName!.StartsWith("oem", StringComparison.OrdinalIgnoreCase)) {
                        collectedInfPaths.Add(infName);
                    }

                    if (!dryRun) {
                        try {
                            DriverUtils.UninstallDevice(devInfo, devInfoData, out var thisNeedsReboot);
                            needsReboot |= thisNeedsReboot;
                        } catch (Exception ex) {
                            Logger.LogFormat(LogLevel.Interactive, "Cannot uninstall device: {0}", ex.ToString());
                        }
                    }
                }
            }

            if (collectedInfPaths.Count > 0) {
                foreach (var oemInfName in collectedInfPaths) {
                    try {
                        DriverUtils.UninstallDriver(oemInfName, dryRun: dryRun);
                    } catch (Exception ex) {
                        Logger.LogFormat(LogLevel.Interactive, "Cannot uninstall driver {0}: {1}", oemInfName, ex.ToString());
                    }
                }
            }
            // Why uninstall everything during DriverInstall?
            // Some older drivers (e.g. old XCP-ng drivers) don't like it when downgraded from a newer version.
            // Remove them all just to be sure.
            // We should arguably require running XenClean first but this covers cases where older drivers are installed
            // after ours.
            DriverUtils.UninstallDriverByNames(dryRun: dryRun, driverName);

            return needsReboot;
        }

        public static void UninstallAllDrivers(bool dryRun) {
            DriverUtils.UninstallDriverByNames(dryRun: dryRun, XenDeviceInfo.KnownDevices.Keys.ToArray());
        }

        public static bool FindAnyDrivers() {
            return DriverUtils.FindDriverByNames(XenDeviceInfo.KnownDevices.Keys.ToArray());
        }

        public static void DeleteAllServices(bool removeNonDriverServices, bool dryRun) {
            using var scm = PInvoke.OpenSCManager((string)null!, null, PInvoke.SC_MANAGER_ALL_ACCESS);
            if (scm.IsInvalid) {
                Logger.Log($"Cannot open SCM: error {Marshal.GetLastWin32Error()}");
                return;
            }
            foreach (var service in DeleteableServices) {
                if (service.Item2 || removeNonDriverServices) {
                    DeleteService(scm, service.Item1, dryRun: dryRun);
                }
            }
        }

        public static void ResetRegistry(bool dryRun) {
            ResetStartOverride(dryRun);

            XenfiltClassCleanup(dryRun);
            ResetAllUnplug(dryRun);

            XenfiltReset(dryRun);
        }

        public static bool IsSafeMode() {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAFEBOOT_OPTION"));
        }
    }
}
