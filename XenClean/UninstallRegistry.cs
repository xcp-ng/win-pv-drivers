using XenDriverUtils;

namespace XenClean {
    internal class UninstallRegistry {
        public static void Execute() {
            XenCleanup.XenbusCleanup();
            XenCleanup.ResetNvmeOverride();
            XenCleanup.XenfiltReset();
            XenCleanup.ResetUnplug();
        }
    }
}
