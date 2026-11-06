using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using WixToolset.Dtf.WindowsInstaller;
using XenDriverUtils;

namespace XenInstCA {
    public class CleanupActions {
        [CustomAction]
        public static ActionResult XenbusCleanup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.XenfiltClassCleanup(dryRun: false);
            XenCleanup.ResetAllUnplug(dryRun: false);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenvbdCleanup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.ResetUnplug(UnplugType.Disks, dryRun: false);
            XenCleanup.ResetStartOverride(dryRun: false);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XennetCleanup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.ResetUnplug(UnplugType.Nics, dryRun: false);
            XenCleanup.ResetStartOverride(dryRun: false);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenRollbackAll(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            foreach (var driverName in new[] {
                "Xenvbd",
                "Xennet",
                "Xenvif",
                "Xenhid",
                "Xenvkbd",
                "Xencons",
                "Xeniface",
                "Xenbus",
            }) {
                try {
                    XenCleanup.UninstallDriver(driverName, dryRun: false);
                } catch (Exception ex) {
                    Logger.LogFormat(LogLevel.Alert, "Cannot uninstall {0}: {1}", driverName, ex.ToString());
                }
            }

            try {
                XenCleanup.DeleteAllServices(removeNonDriverServices: false, dryRun: false);
            } catch (Exception ex) {
                Logger.LogFormat(LogLevel.Alert, "Cannot delete services: {0}", ex.ToString());
            }

            DriverActions.DriverWaitInstallFinish(session);

            XenCleanup.ResetStartOverride(dryRun: false);
            XenCleanup.XenfiltClassCleanup(dryRun: false);
            XenCleanup.ResetAllUnplug(dryRun: false);

            XenOnboard.CaScheduleReboot();

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenfiltReset(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.XenfiltReset(dryRun: false);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenServiceDelete(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            if (!session.CustomActionData.TryGetValue("Services", out var services))
                return ActionResult.Success;
            var serviceList = services.Split(',');

            using var scm = PInvoke.OpenSCManager((string)null, null, PInvoke.SC_MANAGER_ALL_ACCESS);
            if (scm.IsInvalid) {
                Logger.Log(LogLevel.Alert, "Cannot open SCM");
                return ActionResult.Success;
            }
            foreach (var serviceName in serviceList) {
                XenCleanup.DeleteService(scm, serviceName, dryRun: false);
            }

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult RemoveUnwantedDevices(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            try {
                XenOnboard.RemoveUnwantedDevices();
            } catch (Exception ex) {
                Logger.LogFormat(LogLevel.Alert, "Cannot remove unwanted devices: {0}", ex.ToString());
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenvifBackup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            if (XenCleanup.IsSafeMode()) {
                Logger.Log("Skipping Xenvif backup in Safe Mode");
            } else {
                XenOffboard.BackupXenvif(false);
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenvifPrepareRestore(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            if (XenCleanup.IsSafeMode()) {
                Logger.Log("Skipping Xenvif restore in Safe Mode");
            } else {
                XenOffboard.PrepareRestoreXenvif(false);
            }
            return ActionResult.Success;
        }
    }
}
