using WixToolset.Dtf.WindowsInstaller;
using XenDriverUtils;

namespace XenInstCA {
    public class CleanupActions {
        [CustomAction]
        public static ActionResult XenbusCleanup(Session session) {
            XenCleanup.XenbusCleanup();
            XenCleanup.ResetNvmeOverride();
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult XenfiltReset(Session session) {
            XenCleanup.XenfiltReset();
            return ActionResult.Success;
        }
    }
}
