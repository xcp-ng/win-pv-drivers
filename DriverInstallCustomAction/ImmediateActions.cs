using WixToolset.Dtf.WindowsInstaller;
using System;
using System.Linq;
using XenDriverUtils;

namespace XenInstCA {
    public static class ImmediateActions {
        [CustomAction]
        public static ActionResult CheckWindowsVersion(Session session) {
            var minSupportedProperty = session["XenMinSupportedVersion"];
            session.Log($"minSupportedProperty {minSupportedProperty}");
            if (!Version.TryParse(minSupportedProperty, out var minSupported)) {
                return ActionResult.Success;
            }
            session.Log($"minSupported {minSupported}");

            var currentVersion = VersionUtils.GetWindowsVersion();
            session.Log($"currentVersion {currentVersion}");
            if (currentVersion < minSupported) {
                session["XenWindowsNotSupported"] = currentVersion.ToString();
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CheckReboot(Session session) {
            if (XenOnboard.CaIsRebootScheduled()) {
                session.SetMode(InstallRunMode.RebootAtEnd, true);
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CheckIncompatibleDevices(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            var incompatibilities = XenOnboard.FindIncompatibleDevices();
            session["IncompatibleDevices"] = string.Join(",", incompatibilities);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult Check3PStorageDrivers(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            var found3PDrivers = XenCleanup.Find3PStorageDrivers();
            session["ThirdPartyStorageDrivers"] = string.Join(",", found3PDrivers.Select(x => x.DriverInfPath));
            return ActionResult.Success;
        }
    }
}
