using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;
using WixToolset.Dtf.WindowsInstaller;

namespace XNInstCA {
    public class DriverActions {
        [CustomAction]
        public static ActionResult DriverInstall(Session session) {
            var driver = DriverUtils.GetDriverData(session);
            if (driver != null) {
                if (CustomActionUtils.ReportAction(session, $"{driver.DriverName}Install", driver.DriverName) == MessageResult.Cancel) {
                    return ActionResult.UserExit;
                }
                session.Log($"Installing {driver.DriverName} inf {driver.InfPath}");
                if ("Xennet111".Equals(driver.DriverName, StringComparison.OrdinalIgnoreCase)) {
                    bool needsReboot = false;
                    // Preinstall the driver to the driver store
                    unsafe {
                        if (!PInvoke.SetupCopyOEMInf(driver.InfPath, driver.InfPath, OEM_SOURCE_MEDIA_TYPE.SPOST_PATH, 0, null, 0, null, null)) {
                            session.Log($"SetupCopyOEMInf {driver.DriverName} failed {Marshal.GetLastWin32Error()}");
                        }
                    }
                    // TODO: Enumerate
                    unsafe {

                    }
                    if (needsReboot) {
                        CustomActionUtils.ScheduleReboot();
                    }
                } else {
                    BOOL needsReboot;
                    unsafe {
                        PInvoke.DiInstallDriver(HWND.Null, driver.InfPath, 0, &needsReboot);
                    }
                    if (needsReboot) {
                        CustomActionUtils.ScheduleReboot();
                    }
                }
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DriverInstallRollback(Session session) {
            DriverUninstall(session);
            return ActionResult.Success;
        }

        // We're running from the XCP-ng PV driver uninstaller. So we should only remove drivers that belong to our current package and leave the rest alone.
        // Drivers that don't belong to our package should be uninstalled by the standalone uninstaller.
        // We can have a separate installer stage for that.

        // NOTE: we can look up the desired device IDs with InfFile. Do we want to do that?
        // In some cases the drivers in Program Files will desync with that of the driver store for whatever reason (manual update, Windows Update, etc.)
        // If we want to handle these cases then we might just need to uninstall all Xen drivers just in case.
        // The installation would also need to safely handle the case of installing over existing Xen drivers - who would win?

        private class XenDeviceInfo {
            public Guid ClassGuid { get; set; }
            public List<string> CompatibleIds { get; set; }
        }

        private static readonly Dictionary<string, XenDeviceInfo> DevicesToRemove = new(StringComparer.OrdinalIgnoreCase) {
            {
                "Xenbus",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        "PCI\\VEN_5853&DEV_0001",
                        "PCI\\VEN_5853&DEV_0002",
                    }
                }
            },
            {
                "Xencons",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        "XENBUS\\VEN_XN0001&DEV_CONS&REV_09000000",
                        "XENBUS\\VEN_XN0002&DEV_CONS&REV_09000000",
                    }
                }
            },
            {
                "Xenhid",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_HIDCLASS,
                    CompatibleIds = new List<string>() {
                        "XENVKBD\\VEN_XN0001&DEV_HID&REV_09000000",
                        "XENVKBD\\VEN_XN0002&DEV_HID&REV_09000000",
                    }
                }
            },
            {
                "Xeniface",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        "XENBUS\\VEN_XN0001&DEV_IFACE&REV_09000000",
                        "XENBUS\\VEN_XN0002&DEV_IFACE&REV_09000000",
                    }
                }
            },
            {
                "Xennet",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_NET,
                    CompatibleIds = new List<string>() {
                        "XENVIF\\VEN_XN0001&DEV_NET&REV_09000000",
                        "XENVIF\\VEN_XN0002&DEV_NET&REV_09000000",
                    }
                }
            },
            {
                "Xenvbd",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SCSIADAPTER,
                    CompatibleIds = new List<string>() {
                        "XENBUS\\VEN_XN0001&DEV_VBD&REV_09000000",
                        "XENBUS\\VEN_XN0002&DEV_VBD&REV_09000000",
                    }
                }
            },
            {
                "Xenvif",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        "XENBUS\\VEN_XN0001&DEV_VIF&REV_09000000",
                        "XENBUS\\VEN_XN0002&DEV_VIF&REV_09000000",
                    }
                }
            },
            {
                "Xenvkbd",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        "XENBUS\\VEN_XN0001&DEV_VKBD&REV_09000000",
                        "XENBUS\\VEN_XN0002&DEV_VKBD&REV_09000000",
                    }
                }
            },
        };

        [CustomAction]
        public static ActionResult DriverUninstall(Session session) {
            bool needsReboot = false;
            var driver = DriverUtils.GetDriverData(session);
            if (driver == null) {
                return ActionResult.Success;
            }

            session.Log($"Uninstalling {driver.DriverName} inf {driver.InfPath}");
            if (!DevicesToRemove.TryGetValue(driver.DriverName, out var xenInfo)) {
                session.Log($"Unknown driver {driver.DriverName}");
                return ActionResult.Success;
            }

            var devInfo = PInvoke.SetupDiGetClassDevs(xenInfo.ClassGuid, null, HWND.Null, 0);
            var collectedInfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var devInfoData in DriverUtils.EnumerateDevices(devInfo)) {
                List<string> compatibleIds = DriverUtils.GetDeviceCompatibleIds(devInfo, devInfoData);
                if (compatibleIds.Intersect(xenInfo.CompatibleIds, StringComparer.OrdinalIgnoreCase).Count(x => true) == 0) {
                    continue;
                }
                session.Log($"Found device with compatible IDs: [{string.Join(",", compatibleIds)}]");

                List<string> children = DriverUtils.GetDeviceChildren(devInfo, devInfoData);
                foreach (var child in children) {
                    session.Log($"Uninstalling child device {child}");
                    try {
                        var (childInfo, childInfoData) = DriverUtils.OpenDeviceInfo(null, child);
                        unsafe {
                            BOOL thisNeedsReboot;
                            if (!PInvoke.DiUninstallDevice(HWND.Null, childInfo, childInfoData, 0, &thisNeedsReboot)) {
                                session.Log($"DiUninstallDevice error {Marshal.GetLastWin32Error()}");
                                continue;
                            }
                            needsReboot |= thisNeedsReboot;
                        }
                    } catch (Win32Exception ex) {
                        session.Log($"OpenDeviceInfo error {ex.NativeErrorCode}");
                    }
                }

                var infName = DriverUtils.GetDeviceInfPath(devInfo, devInfoData);
                if (infName != null) {
                    if (!string.IsNullOrEmpty(infName)
                            && infName.StartsWith("oem", StringComparison.OrdinalIgnoreCase)) {
                        collectedInfPaths.Add(infName);
                    }
                }

                unsafe {
                    BOOL thisNeedsReboot;
                    if (!PInvoke.DiUninstallDevice(
                            HWND.Null,
                            devInfo,
                            devInfoData,
                            0,
                            &thisNeedsReboot)) {
                        session.Log($"DiUninstallDevice error {Marshal.GetLastWin32Error()}");
                        continue;
                    }
                    needsReboot |= thisNeedsReboot;
                }
            }

            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            foreach (var oemInfName in collectedInfPaths) {
                session.Log($"Uninstalling {oemInfName}");
                var oemInfPath = Path.Combine(windir, "INF", oemInfName);

                BOOL thisNeedsReboot;
                unsafe {
                    if (!PInvoke.DiUninstallDriver(
                            HWND.Null,
                            oemInfPath,
                            0,
                            &thisNeedsReboot)) {
                        session.Log($"DiUninstallDriver error {Marshal.GetLastWin32Error()}");
                        continue;
                    }
                }
                needsReboot |= thisNeedsReboot;

                if (!PInvoke.SetupUninstallOEMInf(oemInfPath, PInvoke.SUOI_FORCEDELETE)) {
                    session.Log($"SetupUninstallOEMInf error, did not cleanly delete devices.");
                }
            }

            if (needsReboot) {
                session.Log("Scheduling reboot");
                CustomActionUtils.ScheduleReboot();
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DriverUninstallRollback(Session session) {
            DriverInstall(session);
            return ActionResult.Success;
        }
    }
}
