using System.Threading;
using WixToolset.Dtf.WindowsInstaller;

namespace XenInstCA {
    public class FakeActions {
        [CustomAction]
        public static ActionResult FakeInstall(Session session) {
            var driver = DriverUtils.GetDriverData(session);
            if (driver != null) {
                if (CustomActionUtils.ReportAction(session, $"{driver.DriverName}Install", driver.DriverName) == MessageResult.Cancel) {
                    return ActionResult.UserExit;
                }
                session.Log($"Installing {driver.DriverName} inf {driver.InfPath}");
                Thread.Sleep(1000);
                CustomActionUtils.ScheduleReboot();
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult FakeInstallRollback(Session session) {
            FakeUninstall(session);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult FakeUninstall(Session session) {
            var driver = DriverUtils.GetDriverData(session);
            if (driver != null) {
                if (CustomActionUtils.ReportAction(session, $"{driver.DriverName}Uninstall", driver.DriverName) == MessageResult.Cancel) {
                    return ActionResult.UserExit;
                }
                session.Log($"Uninstalling {driver.DriverName} inf {driver.InfPath}");
                Thread.Sleep(1000);
                CustomActionUtils.ScheduleReboot();
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult FakeUninstallRollback(Session session) {
            FakeInstall(session);
            return ActionResult.Success;
        }
    }
}
