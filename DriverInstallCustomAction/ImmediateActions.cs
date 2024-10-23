using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using WixToolset.Dtf.WindowsInstaller;
using System;
using System.Collections.Generic;
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
            if (CustomActionUtils.IsRebootScheduled()) {
                session.SetMode(InstallRunMode.RebootAtEnd, true);
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CheckIncompatibleDevices(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));

            var incompatibilities = new List<string>();

            var devInfo = PInvoke.SetupDiGetClassDevs(
                (Guid?)null,
                null,
                HWND.Null,
                SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES);
            foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                List<string> deviceIds = DriverUtils.GetDeviceHardwareAndCompatibleIds(devInfo, devInfoData);
                bool found = false;
                foreach (var xenClass in XenDeviceInfo.KnownDevices.Values) {
                    if (deviceIds.Any(x => xenClass.MatchesId(x, checkKnown: false, checkIncompatible: true))) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    continue;
                }

                Logger.Log($"Found device with incompatible IDs: {string.Join(",", deviceIds)}");
                var instanceId = DriverUtils.GetDeviceInstanceId(devInfo, devInfoData);
                if (instanceId != null) {
                    Logger.Log($"Adding incompatible instance ID {instanceId}");
                    incompatibilities.Add(instanceId);
                } else {
                    incompatibilities.Add("(unknown)");
                }
            }

            session["IncompatibleDevices"] = string.Join(",", incompatibilities);
            return ActionResult.Success;
        }
    }
}
