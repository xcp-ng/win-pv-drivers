using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;

namespace XenDriverUtils {
    public class XenDeviceInfo {
        public Guid? ClassGuid { get; set; }
        public List<string> HardwareIds { get; set; }
        public List<string> IncompatibleIds { get; set; } = new List<string>();

        public bool MatchesId(string deviceId, bool checkKnown, bool checkIncompatible) {
            if (string.IsNullOrEmpty(deviceId))
                return false;
            if (checkKnown && HardwareIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase))
                return true;
            // use a substring match with IncompatibleIds to match all device versions at the same time
            if (checkIncompatible && IncompatibleIds.Any(substr => deviceId.IndexOf(substr, StringComparison.OrdinalIgnoreCase) != -1))
                return true;
            return false;
        }

        public static readonly Dictionary<string, XenDeviceInfo> KnownDevices = new(StringComparer.OrdinalIgnoreCase) {
            {
                "Xenbus",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"PCI\\VEN_5853&DEV_{VersionInfo.VendorDeviceId}&SUBSYS_{VersionInfo.VendorDeviceId}5853&REV_01",
                        // these two entries cover all Xenbus devices
                        "PCI\\VEN_5853&DEV_0001",
                        "PCI\\VEN_5853&DEV_0002",
                    },
                    IncompatibleIds = new List<string>() {
                        // Citrix any version
                        "PCI\\VEN_5853&DEV_C000",
                    }
                }
            },
            {
                "Xencons",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_CONS&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_CONS&REV_09000000",
                    },
                    IncompatibleIds = new List<string>() {
                        // Upstream any version
                        "XENBUS\\VEN_XP0001&DEV_CONS",
                        "XENBUS\\VEN_XP0002&DEV_CONS",
                    }
                }
            },
            {
                "Xenhid",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_HIDCLASS,
                    HardwareIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_HID&REV_09000000",
                        $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}0001&DEV_HID&REV_09000000",
                        $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}0002&DEV_HID&REV_09000000",
                    },
                    IncompatibleIds = new List<string>() {
                        // Upstream any version
                        "XENVKBD\\VEN_XP0001&DEV_HID",
                        "XENVKBD\\VEN_XP0002&DEV_HID",
                    }
                }
            },
            {
                "Xeniface",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_IFACE&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_IFACE&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_IFACE&REV_09000000",
                    },
                    IncompatibleIds = new List<string>() {
                        // Upstream any version
                        "XENBUS\\VEN_XP0001&DEV_IFACE",
                        "XENBUS\\VEN_XP0002&DEV_IFACE",
                        // Citrix any version
                        "XENBUS\\VEN_XSC000&DEV_IFACE",
                        "XENBUS\\VEN_XS0001&DEV_IFACE",
                        "XENBUS\\VEN_XS0002&DEV_IFACE",
                        // XCP-ng v8
                        "XENBUS\\VEN_XNC000&DEV_IFACE&REV_08",
                        "XENBUS\\VEN_XN0001&DEV_IFACE&REV_08",
                        "XENBUS\\VEN_XN0002&DEV_IFACE&REV_08",
                    }
                }
            },
            {
                "Xennet",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_NET,
                    HardwareIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENVIF\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_NET&REV_09000000",
                        $"XENVIF\\VEN_{VersionInfo.VendorPrefix}0001&DEV_NET&REV_09000000",
                        $"XENVIF\\VEN_{VersionInfo.VendorPrefix}0002&DEV_NET&REV_09000000",
                    },
                    IncompatibleIds = new List<string>() {
                        // Upstream any version
                        "XENVIF\\VEN_XP0001&DEV_NET",
                        "XENVIF\\VEN_XP0002&DEV_NET",
                        // Citrix any version
                        "XENVIF\\VEN_XSC000&DEV_NET",
                        "XENVIF\\VEN_XS0001&DEV_NET",
                        "XENVIF\\VEN_XS0002&DEV_NET",
                        // XCP-ng v8
                        "XENVIF\\VEN_XNC000&DEV_NET&REV_08",
                        "XENVIF\\VEN_XN0001&DEV_NET&REV_08",
                        "XENVIF\\VEN_XN0002&DEV_NET&REV_08",
                    }
                }
            },
            {
                "Xenvbd",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SCSIADAPTER,
                    HardwareIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VBD&REV_09000000",
                    },
                    IncompatibleIds = new List<string>() {
                        // Upstream any version
                        "XENBUS\\VEN_XP0001&DEV_VBD",
                        "XENBUS\\VEN_XP0002&DEV_VBD",
                        // Citrix any version
                        "XENBUS\\VEN_XSC000&DEV_VBD",
                        "XENBUS\\VEN_XS0001&DEV_VBD",
                        "XENBUS\\VEN_XS0002&DEV_VBD",
                        // XCP-ng v8
                        "XENBUS\\VEN_XNC000&DEV_VBD&REV_08",
                        "XENBUS\\VEN_XN0001&DEV_VBD&REV_08",
                        "XENBUS\\VEN_XN0002&DEV_VBD&REV_08",
                    }
                }
            },
            {
                "Xenvif",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VIF&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VIF&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VIF&REV_09000000",
                    },
                    IncompatibleIds = new List<string>() {
                        // Upstream any version
                        "XENBUS\\VEN_XP0001&DEV_VIF",
                        "XENBUS\\VEN_XP0002&DEV_VIF",
                        // Citrix any version
                        "XENBUS\\VEN_XSC000&DEV_VIF",
                        "XENBUS\\VEN_XS0001&DEV_VIF",
                        "XENBUS\\VEN_XS0002&DEV_VIF",
                        // XCP-ng v8
                        "XENBUS\\VEN_XNC000&DEV_VIF&REV_08",
                        "XENBUS\\VEN_XN0001&DEV_VIF&REV_08",
                        "XENBUS\\VEN_XN0002&DEV_VIF&REV_08",
                    }
                }
            },
            {
                "Xenvkbd",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VKBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VKBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VKBD&REV_09000000",
                    },
                    IncompatibleIds = new List<string>() {
                        // Upstream any version
                        "XENBUS\\VEN_XP0001&DEV_VKBD",
                        "XENBUS\\VEN_XP0002&DEV_VKBD",
                    }
                }
            },
            // pseudo-devices
            {
                "Xendevice",
                new XenDeviceInfo() {
                    ClassGuid = null,
                    HardwareIds = new List<string>() {
                        "XENDEVICE",
                    }
                }
            },
            {
                "Xenclass",
                new XenDeviceInfo() {
                    ClassGuid = null,
                    HardwareIds = new List<string>() {
                        "XENCLASS",
                    }
                }
            },
        };
    }
}
