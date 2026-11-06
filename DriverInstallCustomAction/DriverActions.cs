using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

            if (CustomActionUtils.ReportActionData1(session, driver.DriverName) == MessageResult.Cancel) {
                return ActionResult.UserExit;
            }
            Logger.Log($"Installing {driver.DriverName} inf {driver.InfPath}");

            DriverUtils.InstallDriver(driver.InfPath, out var needsReboot);
            if (needsReboot) {
                XenOnboard.CaScheduleReboot();
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DriverInstallRollback(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            DriverUninstall(session);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DriverUninstall(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            var driver = GetDriverData(session);
            if (driver == null) {
                return ActionResult.Success;
            }

            if (CustomActionUtils.ReportActionData1(session, driver.DriverName) == MessageResult.Cancel) {
                return ActionResult.UserExit;
            }

            Logger.Log($"Uninstalling {driver.DriverName} inf {driver.InfPath}");
            if (XenCleanup.UninstallDriver(driver.DriverName, dryRun: false)) {
                XenOnboard.CaScheduleReboot();
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DriverUninstallRollback(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            DriverInstall(session);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DriverWaitInstallFinish(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            // wait for up to 1 minute until all pending installations are done
            for (int i = 0; i < 5; i++) {
                // wait twice just to be sure that Windows doesn't decide to install something a second time
                DriverUtils.WaitNoPendingInstallEvents(5000);
                Thread.Sleep(2000);
                if (DriverUtils.WaitNoPendingInstallEvents(5000)) {
                    break;
                }
            }
            return ActionResult.Success;
        }
    }
}
