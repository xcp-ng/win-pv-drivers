using System.Linq;
using XenDriverUtils;

namespace XenClean {
    internal static class UninstallDrivers {
        public static void Execute() {
            DriverUtils.UninstallDriverByNames(XenDeviceInfo.KnownDevices.Keys.ToArray());
        }
    }
}
