using Windows.Win32;
using WixToolset.Dtf.WindowsInstaller;
using XenDriverUtils;

namespace XenInstCA {
    public class CleanupActions {
        [CustomAction]
        public static ActionResult XenbusCleanup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.XenbusCleanup();
            XenCleanup.ResetUnplug();
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenvbdCleanup(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.ResetNvmeOverride();
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenfiltReset(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            XenCleanup.XenfiltReset();
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenServiceDelete(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            if (!session.CustomActionData.TryGetValue("Services", out var services))
                return ActionResult.Success;
            var serviceList = services.Split(',');

            using var scm = PInvoke.OpenSCManager((string)null, (string)null, PInvoke.SC_MANAGER_ALL_ACCESS);
            foreach (var serviceName in serviceList) {
                XenCleanup.DeleteService(scm, serviceName);
            }

            return ActionResult.Success;
        }
    }
}
