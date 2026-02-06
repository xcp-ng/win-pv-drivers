using Windows.Win32;
using WixToolset.Dtf.WindowsInstaller;
using XenDriverUtils;

namespace XenInstCA {
    public class CleanupActions {
        [CustomAction]
        public static ActionResult XenbusCleanup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.XenfiltClassCleanup(dryRun: false);
            XenCleanup.ResetUnplug(dryRun: false);
            XenCleanup.ResetAllForceUnplug(dryRun: false);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenvbdCleanup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.ResetForceUnplug(UnplugType.Disks, dryRun: false);
            XenCleanup.ResetStartOverride(dryRun: false);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XennetCleanup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.ResetForceUnplug(UnplugType.Nics, dryRun: false);
            XenCleanup.ResetStartOverride(dryRun: false);
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
            foreach (var serviceName in serviceList) {
                XenCleanup.DeleteService(scm, serviceName, dryRun: false);
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
