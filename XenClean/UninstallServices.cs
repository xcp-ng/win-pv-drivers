using System.Runtime.InteropServices;
using Windows.Win32;
using XenDriverUtils;

namespace XenClean {
    internal static class UninstallServices {
        public static void Execute(bool removeNonDriverServices, bool dryRun) {
            using var scm = PInvoke.OpenSCManager((string)null, null, PInvoke.SC_MANAGER_ALL_ACCESS);
            if (scm.IsInvalid) {
                Logger.Log($"Cannot open SCM: error {Marshal.GetLastWin32Error()}");
                return;
            }
            foreach (var service in XenCleanup.DeleteableServices) {
                if (service.Item2 || removeNonDriverServices) {
                    XenCleanup.DeleteService(scm, service.Item1, dryRun: dryRun);
                }
            }
        }
    }
}
