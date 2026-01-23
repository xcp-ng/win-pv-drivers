using System.Linq;
using XenDriverUtils;

namespace XenClean {
    internal static class UninstallDrivers {
        public static void Execute(bool dryRun) {
            DriverUtils.UninstallDriverByNames(dryRun: dryRun, XenDeviceInfo.KnownDevices.Keys.ToArray());
        }

        public static bool FindDrivers() {
            return DriverUtils.FindDriverByNames(XenDeviceInfo.KnownDevices.Keys.ToArray());
        }
    }
}
