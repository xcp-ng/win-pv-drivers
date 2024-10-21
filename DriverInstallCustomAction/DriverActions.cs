using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;
using WixToolset.Dtf.WindowsInstaller;
using XenDriverUtils;

namespace XenInstCA {
    internal class DriverData {
        public string DriverName { get; set; }
        public string InfPath { get; set; }
    }

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

            DriverUtils.InstallDriver(driver.InfPath, out var needsReboot);
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

            var devInfo = PInvoke.SetupDiGetClassDevs(
                xenInfo.ClassGuid,
                null,
                HWND.Null,
                xenInfo.ClassGuid.HasValue ? 0 : SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES);
            var collectedInfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                List<string> hardwareIds = DriverUtils.GetDeviceHardwareAndCompatibleIds(devInfo, devInfoData);
                if (hardwareIds.All(x => !xenInfo.MatchesId(x, checkKnown: true, checkIncompatible: false))) {
                    continue;
                }

                var instanceId = DriverUtils.GetDeviceInstanceId(devInfo, devInfoData);
                if (instanceId != null) {
                    Logger.Log($"Found {driver.DriverName} device: {instanceId}");
                } else {
                    Logger.Log($"Found {driver.DriverName} device");
                }

                var infName = DriverUtils.GetDeviceInfPath(devInfo, devInfoData);
                Logger.Log($"Current inf path: {infName}");
                if (!string.IsNullOrEmpty(infName)
                        && infName.StartsWith("oem", StringComparison.OrdinalIgnoreCase)) {
                    collectedInfPaths.Add(infName);
                }

                try {
                    DriverUtils.UninstallDevice(devInfo, devInfoData, out var thisNeedsReboot);
                    needsReboot |= thisNeedsReboot;
                } catch (Exception ex) {
                    Logger.Log($"Cannot uninstall device: {ex.Message}");
                }
            }

            if (collectedInfPaths.Count > 0) {
                foreach (var oemInfName in collectedInfPaths) {
                    try {
                        DriverUtils.UninstallDriver(oemInfName);
                    } catch (Exception ex) {
                        Logger.Log($"Cannot uninstall driver {oemInfName}: {ex.Message}");
                    }
                }
            }
            // Why uninstall everything during DriverInstall?
            // Some older drivers (e.g. old XCP-ng drivers) don't like it when downgraded from a newer version.
            // Remove them all just to be sure.
            // We should arguably require running XenClean first but this covers cases where older drivers are installed after ours.
            DriverUtils.UninstallDriverByNames(driver.DriverName);

            if (needsReboot) {
                CustomActionUtils.ScheduleReboot();
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DriverUninstallRollback(Session session) {
            DriverInstall(session);
            return ActionResult.Success;
        }
    }
}
