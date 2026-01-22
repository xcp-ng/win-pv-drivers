using XenDriverUtils;

namespace XenClean {
    internal class UninstallRegistry {
        public static void Execute(bool dryRun) {
            XenCleanup.ResetStartOverride(dryRun);

            XenCleanup.XenfiltClassCleanup(dryRun);
            XenCleanup.ResetUnplug(dryRun);
            XenCleanup.ResetForceUnplug(dryRun);

            XenCleanup.XenfiltReset(dryRun);
        }
    }
}
