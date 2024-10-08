using System.Threading;
using WixToolset.Dtf.WindowsInstaller;

namespace XNInstCA {
    public class DriverActions {
        [CustomAction]
        public static ActionResult InstallAction(Session session) {
            var driver = DriverUtils.GetDriverData(session);
            if (driver != null) {
                // TODO
                session.Log($"Installing {driver.DriverName} inf {driver.InfPath}");
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult InstallRollback(Session session) {
            UninstallAction(session);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult UninstallAction(Session session) {
            var driver = DriverUtils.GetDriverData(session);
            if (driver != null) {
                // TODO
                session.Log($"Uninstalling {driver.DriverName} inf {driver.InfPath}");
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult UninstallRollback(Session session) {
            // Don't roll back uninstall (i.e. reinstall)
            return ActionResult.Success;
        }
    }
}
