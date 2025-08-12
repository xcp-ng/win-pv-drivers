using XenDriverUtils;

namespace XenClean {
    internal class UninstallRegistry {
        public static void Execute() {
            XenCleanup.ResetStartOverride();

            XenCleanup.XenfiltClassCleanup();
            XenCleanup.ResetUnplug();
            XenCleanup.ResetForceUnplug();

            XenCleanup.XenfiltReset();
        }
    }
}
