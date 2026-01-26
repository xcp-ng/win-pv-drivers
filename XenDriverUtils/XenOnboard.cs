using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

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

        public static List<string> FindIncompatibleDevices() {
            var result = new List<string>();

            using var devInfo = PInvoke.SetupDiGetClassDevs(
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

                Logger.LogFormat(LogLevel.Interactive, "Found device with incompatible IDs: {0}", string.Join(",", deviceIds));
                var instanceId = DriverUtils.GetDeviceInstanceId(devInfo, devInfoData);
                if (instanceId != null) {
                    Logger.LogFormat(LogLevel.Info, "Adding incompatible instance ID {0}", instanceId);
                    result.Add(instanceId);
                } else {
                    result.Add("(unknown)");
                }
            }

            return result;
        }

        public static bool HasIncompatibleXenbus() {
            using var devInfo = PInvoke.SetupDiGetClassDevs(
                (Guid?)null,
                null,
                HWND.Null,
                // we are only interested in present vendor Xenbus (which we can't remove)
                SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES);
            foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                List<string> deviceIds = DriverUtils.GetDeviceHardwareAndCompatibleIds(devInfo, devInfoData);
                if (deviceIds.Any(x => XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(x, checkKnown: false, checkIncompatible: true))) {
                    Logger.LogFormat(LogLevel.Interactive, "Found incompatible Xenbus: {0}", string.Join(",", deviceIds));
                    return true;
                }
            }

            return false;
        }
    }
}
