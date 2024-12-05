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
    }
}
