using System.Runtime.InteropServices;
using Windows.Win32;
using XenDriverUtils;

namespace XenClean {
    internal static class UninstallServices {
        public static void Execute() {
            using var scm = PInvoke.OpenSCManager((string)null, (string)null, PInvoke.SC_MANAGER_ALL_ACCESS);
            if (scm.IsInvalid) {
                Logger.Log($"Cannot open SCM: error {Marshal.GetLastWin32Error()}");
                return;
            }
            foreach (var serviceName in XenCleanup.DeleteableServices) {
                XenCleanup.DeleteService(scm, serviceName);
            }
        }
    }
}
