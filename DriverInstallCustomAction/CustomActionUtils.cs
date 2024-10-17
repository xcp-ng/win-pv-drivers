using WixToolset.Dtf.WindowsInstaller;
using Windows.Win32;
using XenDriverUtils;

namespace XenInstCA {
    internal static class CustomActionUtils {
        private static readonly string RebootScheduledAtomName = "WcaDeferredActionRequiresReboot";

        public static void ScheduleReboot() {
            // Use the same method as Util.wixext's CheckRebootRequired.
            // Ignore errors.
            Logger.Log("Scheduling reboot");
            _ = PInvoke.GlobalAddAtom(RebootScheduledAtomName);
        }

        public static bool IsRebootScheduled() {
            return PInvoke.GlobalFindAtom(RebootScheduledAtomName) != 0;
        }

        public static MessageResult ReportAction(Session session, string actionName, string message) {
            using var action = new Record(2);
            action[1] = actionName;
            action[2] = message;
            return session.Message(InstallMessage.ActionStart, action);
        }
    }
}
