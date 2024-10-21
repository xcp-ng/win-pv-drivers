using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using XenDriverUtils;

namespace XenClean {
    internal static class UninstallDevices {
        static readonly List<string> UninstallOrder = new() {
            "Xenvbd",
            "Xennet",
            "Xenvif",
            "Xenhid",
            "Xenvkbd",
            "Xencons",
            "Xeniface",
            "Xendevice",
            "Xenclass",
            "Xenbus",
        };

        static void RemoveDevices(XenDeviceInfo xenInfo) {
            var devInfo = PInvoke.SetupDiGetClassDevs(
                xenInfo.ClassGuid,
                null,
                HWND.Null,
                xenInfo.ClassGuid.HasValue ? 0 : SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES);
            bool needsReboot = false;

            foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                List<string> compatibleIds = DriverUtils.GetDeviceHardwareAndCompatibleIds(devInfo, devInfoData);
                if (compatibleIds.All(x => !xenInfo.MatchesId(x, checkKnown: true, checkIncompatible: true))) {
                    continue;
                }

                var instanceId = DriverUtils.GetDeviceInstanceId(devInfo, devInfoData);
                if (instanceId != null) {
                    Logger.Log($"Found device: {instanceId}");
                } else {
                    Logger.Log($"Found device");
                }

                try {
                    DriverUtils.UninstallDevice(devInfo, devInfoData, out var thisNeedsReboot);
                    needsReboot |= thisNeedsReboot;
                } catch (Exception ex) {
                    Logger.Log($"Cannot uninstall device: {ex.Message}");
                }
            }
        }

        public static void Execute() {
            foreach (var driver in UninstallOrder) {
                var xenInfo = XenDeviceInfo.KnownDevices[driver];
                Logger.Log($"Uninstalling {driver}");
                RemoveDevices(xenInfo);
            }
        }
    }
}
