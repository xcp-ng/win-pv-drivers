using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;
using WixToolset.Dtf.WindowsInstaller;

namespace XenInstCA {
    public class DriverActions {
        private static DriverData GetDriverData(Session session) {
            if (!session.CustomActionData.TryGetValue("Driver", out var driverName)) return null;
            if (!session.CustomActionData.TryGetValue("Inf", out var infPath)) return null;
            Logger.Log($"driverName {driverName} infPath {infPath}");
            return new DriverData() {
                DriverName = driverName,
                InfPath = infPath,
            };
        }

        [CustomAction]
        public static ActionResult DriverInstall(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            var driver = GetDriverData(session);
            if (driver == null) {
                return ActionResult.Success;
            }

            if (CustomActionUtils.ReportAction(session, $"{driver.DriverName}Install", driver.DriverName) == MessageResult.Cancel) {
                return ActionResult.UserExit;
            }
            Logger.Log($"Installing {driver.DriverName} inf {driver.InfPath}");

            if (!XenDeviceInfo.KnownDevices.TryGetValue(driver.DriverName, out var xenInfo)) {
                throw new NotSupportedException($"Unknown driver {driver.DriverName}");
            }

            bool needsReboot;
            if (xenInfo.SafeInstall) {
                DriverUtils.InstallDriverSafe(driver, xenInfo, out needsReboot);
            } else {
                DriverUtils.InstallDriver(driver, out needsReboot);
            }

            if (needsReboot) {
                CustomActionUtils.ScheduleReboot();
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DriverInstallRollback(Session session) {
            DriverUninstall(session);
            return ActionResult.Success;
        }

        // We're running on behalf of the PV driver uninstaller. We should only remove drivers belonging to our current package and leave the rest alone.
        // Drivers that don't belong to our package should be uninstalled by the standalone uninstaller.
        // We can have a separate installer stage for that.

        // NOTE: we can look up the desired device IDs with InfFile. Do we want to do that?
        // In some cases the drivers in Program Files will desync with that of the driver store for whatever reason (manual update, Windows Update, etc.)
        // If we want to handle these cases then we might just need to uninstall all Xen drivers just in case.
        // The installation would also need to safely handle the case of installing over existing Xen drivers - who would win?

        [CustomAction]
        public static ActionResult DriverUninstall(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            bool needsReboot = false;
            var driver = GetDriverData(session);
            if (driver == null) {
                return ActionResult.Success;
            }

            if (CustomActionUtils.ReportAction(session, $"{driver.DriverName}Uninstall", driver.DriverName) == MessageResult.Cancel) {
                return ActionResult.UserExit;
            }

            Logger.Log($"Uninstalling {driver.DriverName} inf {driver.InfPath}");
            if (!XenDeviceInfo.KnownDevices.TryGetValue(driver.DriverName, out var xenInfo)) {
                Logger.Log($"Unknown driver {driver.DriverName}");
                return ActionResult.Success;
            }

            var devInfo = PInvoke.SetupDiGetClassDevs(xenInfo.ClassGuid, null, HWND.Null, 0);
            var collectedInfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                List<string> compatibleIds = DriverUtils.GetDeviceCompatibleIds(devInfo, devInfoData);
                // Enumerable.All is true also for empty enumerables
                if (compatibleIds
                        .Intersect(xenInfo.CompatibleIds, StringComparer.OrdinalIgnoreCase)
                        .All(x => string.IsNullOrEmpty(x))) {
                    continue;
                }
                Logger.Log($"Found device with compatible IDs: {string.Join(",", compatibleIds)}");

                var infName = DriverUtils.GetDeviceInfPath(devInfo, devInfoData);
                Logger.Log($"Current inf path: {infName}");
                if (!string.IsNullOrEmpty(infName)
                        && infName.StartsWith("oem", StringComparison.OrdinalIgnoreCase)) {
                    collectedInfPaths.Add(infName);
                }

                unsafe {
                    BOOL thisNeedsReboot;
                    if (PInvoke.DiUninstallDevice(
                            HWND.Null,
                            devInfo,
                            devInfoData,
                            0,
                            &thisNeedsReboot)) {
                        needsReboot |= thisNeedsReboot;
                    } else {
                        Logger.Log($"DiUninstallDevice error {Marshal.GetLastWin32Error()}");
                        continue;
                    }
                }
            }

            if (collectedInfPaths.Count > 0) {
                foreach (var oemInfName in collectedInfPaths) {
                    UninstallOemInf(session, oemInfName);
                }
            } else {
                var baseName = Path.GetFileNameWithoutExtension(driver.InfPath);
                if (string.IsNullOrEmpty(baseName)) {
                    return ActionResult.Success;
                }
                var wantedCatalogName = $"{baseName}.cat";
                Logger.Log($"Didn't find {driver.DriverName} devices; uninstalling by catalog name {wantedCatalogName}");
                var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var infdir = Path.Combine(windir, "inf");
                foreach (var infPath in Directory.EnumerateFiles(infdir, "oem*.inf", SearchOption.TopDirectoryOnly)) {
                    if (!".inf".Equals(Path.GetExtension(infPath), StringComparison.OrdinalIgnoreCase)) {
                        // netfx bug when using asterisks
                        continue;
                    }
                    try {
                        using var infFile = InfFile.Open(infPath, null, INF_STYLE.INF_STYLE_WIN4, out _);
                        var infCatalog = infFile.GetStringField("Version", "CatalogFile", 1);
                        var infProvider = infFile.GetStringField("Version", "Provider", 1);
                        if (!wantedCatalogName.Equals(infCatalog, StringComparison.OrdinalIgnoreCase)
                            || !XenInstCA.Version.VendorName.Equals(infProvider, StringComparison.OrdinalIgnoreCase)) {
                            continue;
                        }
                    } catch (Exception ex) {
                        Logger.Log($"Cannot parse {infPath}: {ex.Message}");
                    }
                    var oemInfName = Path.GetFileName(infPath);
                    UninstallOemInf(session, oemInfName);
                }
            }

            if (needsReboot) {
                CustomActionUtils.ScheduleReboot();
            }
            return ActionResult.Success;
        }

        private static void UninstallOemInf(Session session, string oemInfName) {
            Logger.Log($"Uninstalling {oemInfName}");
            if (!PInvoke.SetupUninstallOEMInf(oemInfName, PInvoke.SUOI_FORCEDELETE)) {
                Logger.Log($"SetupUninstallOEMInf error, did not cleanly delete driver.");
            }
        }

        [CustomAction]
        public static ActionResult DriverUninstallRollback(Session session) {
            DriverInstall(session);
            return ActionResult.Success;
        }
    }
}
