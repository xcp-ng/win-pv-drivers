using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
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
            foreach (var serviceName in XenDriverUtils.XenCleanup.DeleteableServices) {
                XenDriverUtils.XenCleanup.DeleteService(scm, serviceName);
            }
        }
    }
}
