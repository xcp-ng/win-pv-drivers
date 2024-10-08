using System.Collections.Generic;
using System.Runtime.InteropServices;
using WixToolset.Dtf.WindowsInstaller;
using Windows.Win32;

namespace XNInstCA {
    internal static class CustomActionUtils {
        private static readonly string RebootScheduledAtomName = "WcaDeferredActionRequiresReboot";

        public static void ScheduleReboot() {
            // Use the same method as Util.wixext's CheckRebootRequired.
            // Ignore errors.
            _ = PInvoke.GlobalAddAtom(RebootScheduledAtomName);
        }

        public static bool IsRebootScheduled() {
            return PInvoke.GlobalFindAtom(RebootScheduledAtomName) != 0;
        }
    }
}
