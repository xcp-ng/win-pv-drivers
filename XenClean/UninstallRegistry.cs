using XenDriverUtils;

namespace XenClean {
    internal class UninstallRegistry {
        public static void Execute() {
            XenCleanup.XenfiltClassCleanup();
            XenCleanup.XenfiltReset();
            XenCleanup.ResetStartOverride();
            XenCleanup.ResetUnplug();
        }
    }
}
