using XenDriverUtils;

namespace XenClean {
    internal class UninstallRegistry {
        public static void Execute() {
            XenCleanup.XenfiltClassCleanup();
            XenCleanup.ResetNvmeOverride();
            XenCleanup.ResetUnplug();
        }
    }
}
