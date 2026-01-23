using Windows.Win32;

namespace XenDriverUtils {
    public static class XenOnboard {
        const string RebootScheduledAtomName = "WcaDeferredActionRequiresReboot";

        public static void CaScheduleReboot() {
            // Use the same method as Util.wixext's CheckRebootRequired.
            // Ignore errors.
            Logger.Log("Scheduling reboot");
            _ = PInvoke.GlobalAddAtom(RebootScheduledAtomName);
        }

        public static bool CaIsRebootScheduled() {
            return PInvoke.GlobalFindAtom(RebootScheduledAtomName) != 0;
        }
    }
}
