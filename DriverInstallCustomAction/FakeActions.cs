using System.Configuration;
using System.Threading;
using WixToolset.Dtf.WindowsInstaller;

namespace XNInstCA {
    public class FakeActions {
        [CustomAction]
        public static ActionResult FakeInstall(Session session) {
            var driver = DriverUtils.GetDriverData(session);
            if (driver != null) {
                using (var action = new Record(3)) {
                    action[1] = $"{driver.DriverName}Install";
                    action[2] = driver.DriverName;
                    action[3] = "I'm installing driver: [1]; [2]; [3]";
                    if (session.Message(InstallMessage.ActionStart, action) == MessageResult.Cancel) {
                        return ActionResult.UserExit;
                    }
                }
                Thread.Sleep(1000);
                using (var action = new Record(3)) {
                    action[1] = $"{driver.DriverName}Install";
                    action[2] = $"data({driver.DriverName})";
                    action[3] = $"data({driver.InfPath})";
                    if (session.Message(InstallMessage.ActionData, action) == MessageResult.Cancel) {
                        return ActionResult.UserExit;
                    }
                }
                Thread.Sleep(1000);
                session.Log($"Installing {driver.DriverName} inf {driver.InfPath}");
            }
            CustomActionUtils.ScheduleReboot();
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
                using (var action = new Record(2)) {
                    action[1] = $"{driver.DriverName}Uninstall";
                    action[2] = driver.DriverName;
                    if (session.Message(InstallMessage.ActionStart, action) == MessageResult.Cancel) {
                        return ActionResult.UserExit;
                    }
                }
                Thread.Sleep(1000);
                session.Log($"Uninstalling {driver.DriverName} inf {driver.InfPath}");
            }
            CustomActionUtils.ScheduleReboot();
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult FakeUninstallRollback(Session session) {
            // Don't roll back uninstall (i.e. reinstall)
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult FakeProgress(Session session) {
            if (session.GetMode(InstallRunMode.Scheduled)) {
                using var actionRec = new Record(3);
                using var progressRec = new Record(3);

                actionRec[1] = "FakeProgressDeferred";
                actionRec[2] = "Fake install";
                actionRec[3] = "Fake installing [1] of [2]";
                if (session.Message(InstallMessage.ActionStart, actionRec) == MessageResult.Cancel) {
                    return ActionResult.UserExit;
                }

                progressRec[1] = 1;
                progressRec[2] = 1;
                progressRec[3] = 0;
                if (session.Message(InstallMessage.Progress, progressRec) == MessageResult.Cancel) {
                    return ActionResult.UserExit;
                }

                progressRec[1] = 2;
                progressRec[2] = 12;
                progressRec[3] = 0;
                actionRec[2] = 100;

                for (int i = 0; i < 100; i += 12) {
                    actionRec[1] = i;
                    if (session.Message(InstallMessage.ActionData, actionRec) == MessageResult.Cancel) {
                        return ActionResult.UserExit;
                    }
                    if (session.Message(InstallMessage.Progress, progressRec) == MessageResult.Cancel) {
                        return ActionResult.UserExit;
                    }
                    Thread.Sleep(500);
                }

                return ActionResult.Success;
            } else {
                using var progressRec = new Record(2);
                progressRec[1] = 3;
                progressRec[2] = 100;
                if (session.Message(InstallMessage.Progress, progressRec) == MessageResult.Cancel) {
                    return ActionResult.UserExit;
                }

                return ActionResult.Success;
            }
        }
    }
}
