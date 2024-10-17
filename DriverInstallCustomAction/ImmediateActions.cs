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
        public static ActionResult CheckReboot(Session session) {
            if (CustomActionUtils.IsRebootScheduled()) {
                session.SetMode(InstallRunMode.RebootAtEnd, true);
            }
            return ActionResult.Success;
        }

        private static readonly List<string> IncompatibleIds = new() {
            // Citrix
            "PCI\\VEN_5853&DEV_C000",
        };

        [CustomAction]
        public static ActionResult CheckIncompatibleDevices(Session session) {
            var incompatibilities = new List<string>();

            var devInfo = PInvoke.SetupDiGetClassDevs(
                (Guid?)null,
                null,
                HWND.Null,
                SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT);
            foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                List<string> compatibleIds = DriverUtils.GetDeviceHardwareAndCompatibleIds(devInfo, devInfoData);
                // Enumerable.All is true also for empty enumerables
                if (compatibleIds
                        .Intersect(IncompatibleIds, StringComparer.OrdinalIgnoreCase)
                        .All(x => string.IsNullOrEmpty(x))) {
                    continue;
                }
                Logger.Log($"Found device with incompatible IDs: {string.Join(",", compatibleIds)}");

                var instanceId = DriverUtils.GetDeviceInstanceId(devInfo, devInfoData);
                if (instanceId != null) {
                    incompatibilities.Add(instanceId);
                } else {
                    incompatibilities.Add("(unknown)");
                }
            }

            session["IncompatibleDevices"] = string.Join(", ", incompatibilities);
            return ActionResult.Success;
        }
    }
}
